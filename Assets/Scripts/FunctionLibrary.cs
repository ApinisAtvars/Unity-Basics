using UnityEngine;

using static UnityEngine.Mathf;

public static class FunctionLibrary // Static keyword means class can't be used as an object template
{
    public delegate Vector3 Function (float x, float z, float t);
    private static Function[] functions = { Wave, MultiWave, Ripple, Sphere, Torus };
    public enum FunctionName { Wave, MultiWave, Ripple, Sphere, Torus };

    public static int FunctionCount => functions.Length;

    public static Function GetFunction (FunctionName name) => functions[(int)name];

	public static FunctionName GetNextFunctionName (FunctionName name) =>
		(int)name < functions.Length - 1 ? name + 1 : 0;

    public static FunctionName GetRandomFunctionNameOtherThan (FunctionName name) {
		var choice = (FunctionName)Random.Range(1, functions.Length);
		return choice == name ? 0 : choice;
	}

    public static Vector3 Morph (
		float u, float v, float t, Function from, Function to, float progress
	)
    {
        return Vector3.LerpUnclamped( // No need to clamp as SmoothStep clamps already
			from(u, v, t), to(u, v, t), SmoothStep(0f, 1f, progress)
		);
    }

    // Static here means they can be invoked on the class, not instance level
    public static Vector3 Wave (float u, float v, float t)
    {
        Vector3 p;
        p.x = u;
        p.y = Sin(PI * (u + v + t));
        p.z = v;

        return p;
    }

    public static Vector3 MultiWave (float u, float v, float t)
    {
        Vector3 p;
        p.x = u;
        p.y = Sin(PI * (u + 0.5f + t));
        // Multiplication is faster than division
        // By including literals in parantheses, compiler recognizes them as constexpr,
        // and calculates them at compile time.

        // You can also just multiply by 0.5f
        p.y += Sin(2f * PI * (v+t)) * (1f / 2f);
        p.y += Sin(PI * (u + v + 0.25f * t));
        p.y *= 1f / 2.5f;

        p.z = v;

        return p;
    }

    public static Vector3 Ripple (float u, float v, float t)
    {
        float d = Sqrt(u * u + v * v);
        Vector3 p;

        p.x = u;
        p.y = Sin(PI * (4f * d - t));
        p.y /= 1f + 10f * d;
        p.z = v;

        
        return p;
    }

    public static Vector3 Sphere (float u, float v, float t)
    {
        float r = 0.9f + 0.1f * Sin(PI * (6f * u + 4f * v + t));
		float s = r * Cos(0.5f * PI * v);
        Vector3 p;
        p.x = s * Sin(PI * u);
        p.y = r * Sin(PI * 0.5f * v);
        p.z = s * Cos(PI * u);

        return p;
    }

    public static Vector3 Torus (float u, float v, float t)
    {
        float major_radius = 0.7f + 0.1f * Sin(PI * (6f * u + 0.5f * t));;
        float minor_radius = 0.15f + 0.05f * Sin(PI * (8f * u + 4f * v + 2f * t));;
		float s = major_radius + minor_radius * Cos(PI * v);
		Vector3 p;
		p.x = s * Sin(PI * u);
		p.y = minor_radius * Sin(PI * v);
		p.z = s * Cos(PI * u);
		return p;
    }
}
