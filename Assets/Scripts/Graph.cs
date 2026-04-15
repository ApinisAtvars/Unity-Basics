using UnityEngine;

public class Graph : MonoBehaviour
{

    [SerializeField]
    private Transform pointPrefab;
    // Range instructs the inspector to create a slider for this attribute
    [SerializeField, Range(10, 200)]
    [Tooltip("Number of points that will be instantiated")]
    private int resolution = 10;

    [SerializeField]
    private FunctionLibrary.FunctionName function;

    public enum TransitionMode { Cycle, Random }

	[SerializeField]
	TransitionMode transitionMode;

    [SerializeField, Min(0f)]
	private float functionDuration = 1f, transitionDuration = 1f;

    private Transform[] points;

    private float duration;
    private bool transitioning;

	FunctionLibrary.FunctionName transitionFunction;
    
    void Awake () {
        float step = 2f / resolution;
        var position = Vector3.zero;
		var scale = Vector3.one * step;
        points = new Transform[resolution * resolution]; // Set the length of the array
		for (int i = 0; i < points.Length; i++) {

			Transform point = points[i] = Instantiate(pointPrefab);
            
            // position.y = position.x * position.x;
			point.localPosition = position;
			point.localScale = scale;
            /*
            We don't need the children to stay at the same world position, rotation and scale
            since that is already the default for the parent.

            So, we can skip some calculations by setting the second parameter to false.
            */
            point.SetParent(transform, false);
		}
	}
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
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
        
        if (transitioning) {
			UpdateFunctionTransition();
		}
		else {
			UpdateFunction();
		}
    }

    void PickNextFunction () {
		function = transitionMode == TransitionMode.Cycle ?
			FunctionLibrary.GetNextFunctionName(function) :
			FunctionLibrary.GetRandomFunctionNameOtherThan(function);
	}

    void UpdateFunction()
    {
        float time = Time.time;
        FunctionLibrary.Function f = FunctionLibrary.GetFunction(function);

        float step = 2f / resolution;
        float v = 0.5f * step - 1f; // v only needs to be recalculated when z changes
		for (int i = 0, x = 0, z = 0; i < points.Length; i++, x++) {
			if (x == resolution) {
				x = 0;
				z += 1;
                v = (z + 0.5f) * step - 1f;
			}
			float u = (x + 0.5f) * step - 1f;
			points[i].localPosition = f(u, v, time);
		}
    }

    void UpdateFunctionTransition()
    {
        float time = Time.time;
        FunctionLibrary.Function
			from = FunctionLibrary.GetFunction(transitionFunction),
			to = FunctionLibrary.GetFunction(function);
		float progress = duration / transitionDuration;

        float step = 2f / resolution;
        float v = 0.5f * step - 1f; // v only needs to be recalculated when z changes
		for (int i = 0, x = 0, z = 0; i < points.Length; i++, x++) {
			if (x == resolution) {
				x = 0;
				z += 1;
                v = (z + 0.5f) * step - 1f;
			}
			float u = (x + 0.5f) * step - 1f;
			points[i].localPosition = FunctionLibrary.Morph(
				u, v, time, from, to, progress
			);
		}
    }

    
}
