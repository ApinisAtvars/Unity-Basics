using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics; // Burst is optimized to work with this library

using UnityEngine;

using static Unity.Mathematics.math;

using quaternion = Unity.Mathematics.quaternion;
using Random = UnityEngine.Random;

public class Fractal : MonoBehaviour
{
    // Interface specifically optimized for jobs running in for loops
    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)] // This actually gives performance impact that jobs can provide
    private struct UpdateFractalLevelJob : IJobFor
    {
        public float spinAngleDelta;
		public float scale;

        [ReadOnly] // Not needed but used to explicitly indicate this
		public NativeArray<FractalPart> parents;
		public NativeArray<FractalPart> parts;

        [WriteOnly] // same here
		public NativeArray<float3x4> matrices;

        // This replaces the code that used to be in the inner most loop of the Update function
        public void Execute (int i)
        {
            // Works because integer division automatically rounds to nearest int
            FractalPart parent = parents[i / 5];
            FractalPart part = parts[i];

            part.spinAngle += spinAngleDelta;

            // For Quaternions, rotation stacking is weird as hell
            // If you do it like this (correctly), the child gets rotated first\
            // and then the parent gets rotated. odd...
            part.worldRotation = 
                mul(parent.worldRotation,
                mul(part.rotation
                    ,quaternion.Euler(0f, part.spinAngle, 0f)));

            // Does this formatting make sense? I can't decide.
            
            part.worldPosition = 
                parent.worldPosition 
                + mul(parent.worldRotation // Parent's rotation should affect the direction of the child's offset.
                , (1.5f * scale
                    * part.direction));

            parts[i] = part;

            float3x3 r = float3x3(part.worldRotation) * scale;
            matrices[i] = float3x4(r.c0, r.c1, r.c2, part.worldPosition);
        }
    }

    [SerializeField, Range(2, 8)]
    private int depth = 4;

    [SerializeField]
    private Mesh mesh;

    [SerializeField]
    private Material material;

    private static float3[] directions =
    {
        up(), right(), left(), forward(), back()
    };

    private static quaternion[] rotations =
    {
        quaternion.identity,
        quaternion.RotateZ(-0.5f * PI), quaternion.RotateZ(0.5f * PI),
		quaternion.RotateX(0.5f * PI), quaternion.RotateX(-0.5f * PI)
    };

    /*
    Instead of updating every GO from itself, we update them from the root object.
    From Unity side, this means it has to only update a single GO instead of thousands.

    To do this, we need to keep track of the data for all parts in a single component.
    */
    private struct FractalPart
    {
        // public here only exposes the fields inside Fractal scope because the struct is still private
        public float3 direction, worldPosition;
        public quaternion rotation, worldRotation;
        // To start with a fresh quaternion each update
        // so that the floating point errors of tiny rotations don't accumulate.
        public float spinAngle;
    }

    // Give all parts on the same level their own array, an array of arrays
    NativeArray<FractalPart>[] parts;

    // Array of all transformation matrices, since we don't store the transforms themselves for each part
    NativeArray<float3x4>[] matrices;

    // Since we don't store transforms, it's our job now to send matrices to GPU to render them
    ComputeBuffer[] matricesBuffers;
    private static readonly int 
        matricesId = Shader.PropertyToID("_Matrices"),
        sequenceNumbersId = Shader.PropertyToID("_SequenceNumbers"),
        colorAId = Shader.PropertyToID("_ColorA"),
        colorBId = Shader.PropertyToID("_ColorB");
    // Needed for linking each buffer to a specific draw command.
    // Otherwise, all levels get rendered using the matrices of the last level,
    // thus rendering only the last level however many times
    private static MaterialPropertyBlock propertyBlock;

    [SerializeField]
    private Gradient gradientA, gradientB;
    private Vector4[] sequenceNumbers;


    FractalPart CreatePart (int childIndex) => new FractalPart {
		direction = directions[childIndex],
		rotation = rotations[childIndex]
	};

    void OnEnable()
    {
        sequenceNumbers = new Vector4[depth];

        parts = new NativeArray<FractalPart>[depth];
        matrices = new NativeArray<float3x4>[depth];

        matricesBuffers = new ComputeBuffer[depth];
        int stride = 12 * 4; // 3x4 matrix = 12 values, filled with 4 byte floats, so 12 * 4

        // 5 directions, 5 children for every fractal
        for (int i = 0, length = 1; i < parts.Length; i++, length *= 5)
        {
            // Second argument defines how long the arrays should persist for
            // Since we use them every frame, this is the correct parameter
            parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent);
            matrices[i] = new NativeArray<float3x4>(length, Allocator.Persistent);
            matricesBuffers[i] = new ComputeBuffer(length, stride);

            sequenceNumbers[i] = new Vector4(Random.value, Random.value);
        }

        parts[0][0] = CreatePart(0);
        for (int li = 1; li < parts.Length; li++) // For each level (level index)
        {
            NativeArray<FractalPart> levelParts = parts[li]; // Store the reference to the parts array for the level
            for (int fpi = 0; fpi < levelParts.Length; fpi += 5) // For all parts in level
            {
                for (int ci = 0; ci < 5; ci++)
                {
                    levelParts[fpi + ci] = CreatePart(ci);
                }
            }
        }

        propertyBlock ??= new MaterialPropertyBlock(); // Same as if (null) {...}
    }

    void OnDisable()
    {
        for (int i = 0; i < matricesBuffers.Length; i++)
        {
            matricesBuffers[i].Release();
            parts[i].Dispose();
            matrices[i].Dispose();
        }
        parts = null;
        matrices = null;
        matricesBuffers = null;
        sequenceNumbers = null;
    }

    // This enables changing the fractal depth via inspector while in play mode
    void OnValidate()
    {
        if (parts != null && enabled) // enabled because it gets invoked while the component gets disabled too
        {
            OnDisable();
            OnEnable();
        }
    }

    void Update()
    {
        float spinAngleDelta = 0.125f * PI * Time.deltaTime;

        FractalPart rootPart = parts[0][0];

        rootPart.spinAngle += spinAngleDelta;
        // Parentheses needed because you first apply the quaternion, then the root part's rotation, then the transform's rotation
        rootPart.worldRotation = 
            mul(transform.rotation
            , mul(rootPart.rotation, quaternion.Euler(0f, rootPart.spinAngle, 0f)));
        parts[0][0] = rootPart;

        // lossyScale because it might be non-affine due to it being in a complex hierarchy that includes non-uniform scales with rotations 
        float objectScale = transform.lossyScale.x;
        float3x3 r = float3x3(rootPart.worldRotation) * objectScale;
		matrices[0][0] = float3x4(r.c0, r.c1, r.c2, rootPart.worldPosition);
        float scale = objectScale;

        JobHandle jobHandle = default;

        for (int li = 1; li < parts.Length; li++)
        {
            scale *= 0.5f;

            jobHandle = new UpdateFractalLevelJob {
                spinAngleDelta = spinAngleDelta,
                scale = scale,
                parents = parts[li - 1],
                parts = parts[li],
                matrices = matrices[li]
            }.ScheduleParallel(parts[li].Length, 5, jobHandle);
            // Instead of a for loop, we schedule it so it performs the loop on its own
            // All parts on the same level are independent of each other, so we can process them in parallel

        }

        // Complete tells the program to wait until execution is complete
        jobHandle.Complete();

        var bounds = new Bounds(rootPart.worldPosition, 3f * objectScale * Vector3.one);
        for (int i = 0; i < matricesBuffers.Length; i++)
        {
            ComputeBuffer buffer = matricesBuffers[i];
            buffer.SetData(matrices[i]);
            // Evaluate both gradients and set the propertyBlock colors
            float gradientInterpolator = i / (matricesBuffers.Length - 1f);
			propertyBlock.SetColor(colorAId, gradientA.Evaluate(gradientInterpolator));
			propertyBlock.SetColor(colorBId, gradientB.Evaluate(gradientInterpolator));
            // By passing this as an extra argument for DrawMeshInstanedProcedural,
            // it makes Unity copy the configuration that this block has at this specific time
            // thus solving the issue of all layers being rendered using the meshes of the final layer.
            propertyBlock.SetBuffer(matricesId, buffer);

            propertyBlock.SetVector(sequenceNumbersId, sequenceNumbers[i]);

            Graphics.DrawMeshInstancedProcedural(
                mesh, 0, material, bounds, buffer.count, propertyBlock
            );
        }
    }

}
