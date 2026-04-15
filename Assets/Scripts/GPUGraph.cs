using UnityEngine;
using UnityEngine.UIElements;

public class GPUGraph : MonoBehaviour
{
    private const int maxResolution = 1000;
    // Range instructs the inspector to create a slider for this attribute
    [SerializeField, Range(10, maxResolution)]
    [Tooltip("Number of points that will be instantiated")]
    private int resolution = 10;

    [SerializeField]
    private FunctionLibrary.FunctionName function;

    public enum TransitionMode { Cycle, Random }

	[SerializeField]
	TransitionMode transitionMode;

    [SerializeField, Min(0f)]
	private float functionDuration = 1f, transitionDuration = 1f;

    private float duration;
    private bool transitioning;

	FunctionLibrary.FunctionName transitionFunction;

    private ComputeBuffer positionsBuffer; // For storing pos on GPU

    [SerializeField]
    private ComputeShader computeShader;

    static readonly int // Provide the parameters required by the compute shader
		positionsId = Shader.PropertyToID("_Positions"),
		resolutionId = Shader.PropertyToID("_Resolution"),
		stepId = Shader.PropertyToID("_Step"),
		timeId = Shader.PropertyToID("_Time"),
        transitionProgressId = Shader.PropertyToID("_TransitionProgress");

    
    [SerializeField]
    private Material material;

    [SerializeField]
    private Mesh mesh;

    void OnEnable ()
    {
        /*
        We need to store resolution^2 point positions
        Each point position is a 3D float vector
        Float is 4 bytes => 3 * 4
        */
        positionsBuffer = new ComputeBuffer(maxResolution * maxResolution, 3 * 4);
    }

    void OnDisable () {
		positionsBuffer.Release(); // It will automatically release at some point, but it's better to to it explicitly
        positionsBuffer = null;
	}
    

    // Update is called once per frame
    void Update()
    {
        duration += Time.deltaTime;
		if (transitioning)
        {
            if (duration >= transitionDuration) {
				duration -= transitionDuration;
				transitioning = false;
			}
        }
		else if (duration >= functionDuration) 
        {
			duration -= functionDuration;
			transitioning = true;
			transitionFunction = function;
			PickNextFunction();
		}

        UpdateFunctionOnGPU();
    }

    void PickNextFunction () {
		function = transitionMode == TransitionMode.Cycle ?
			FunctionLibrary.GetNextFunctionName(function) :
			FunctionLibrary.GetRandomFunctionNameOtherThan(function);
	}

    void UpdateFunctionOnGPU () {
		float step = 2f / resolution;
		computeShader.SetInt(resolutionId, resolution);
		computeShader.SetFloat(stepId, step);
		computeShader.SetFloat(timeId, Time.time);
        if (transitioning)
        {
            computeShader.SetFloat(
                transitionProgressId,
                Mathf.SmoothStep(0f, 1f, duration / transitionDuration) // Only calculate the step once per frame
            );
        }

        // We have each possible function type as a separate kernel.
        var kernelIndex =
			(int)function +
			(int)(transitioning ? transitionFunction : function) *
			FunctionLibrary.FunctionCount;
        
        computeShader.SetBuffer(kernelIndex, positionsId, positionsBuffer);

        /*
        Now, run the kernel by calling Dispatch.

        Because the kernel has a fixed size of 8x8, we need the smallest integer
        which fits the resolution / 8.
        */
        int groups = Mathf.CeilToInt(resolution / 8f);
        computeShader.Dispatch(kernelIndex, groups, groups, 1);

        /*
        Here we draw the objects.
        But since we're not working with GameObjects, Unity has no idea where to draw them.

        We have to indicate where the drawing happens by providing a bounding box.

        Everything outside of the bounds gets culled.
        */
        material.SetBuffer(positionsId, positionsBuffer);
		material.SetFloat(stepId, step);
        var bounds = new Bounds(Vector3.zero, Vector3.one * (2f + 2f / resolution));
        Graphics.DrawMeshInstancedProcedural(
			mesh, 0, material, bounds, resolution * resolution
		);
	}

    
}
