#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
	StructuredBuffer<float3> _Positions;
#endif

float _Step;

void ConfigureProcedural () {
	#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
		float3 position = _Positions[unity_InstanceID];

		unity_ObjectToWorld = 0.0;
		unity_ObjectToWorld._m03_m13_m23_m33 = float4(position, 1.0);
		unity_ObjectToWorld._m00_m11_m22 = _Step;
	#endif
}

/*
We need a dummy function to include the code into our shader graph.

The _float suffix indicates the precision of the function, it is needed.
*/
void ShaderGraphFunction_float (float3 In, out float3 Out) {
	Out = In;
}

// Ensure the graph will work with both precision modes.
void ShaderGraphFunction_half (half3 In, out half3 Out) {
	Out = In;
}
