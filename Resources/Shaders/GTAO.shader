Shader "Hidden/GroundTruthAmbientOcclusion"
{
	HLSLINCLUDE
		#include "GTAO_Pass.hlsl"
	ENDHLSL

	SubShader
	{
		ZTest Always
		Cull Off
		ZWrite Off

		Pass 
		{ 
			Name"ResolveGTAO"
			HLSLPROGRAM 
				#pragma vertex vert
				#pragma fragment ResolveGTAO_frag
			ENDHLSL 
		}

		Pass 
		{ 
			Name"SpatialGTAO_X"
			HLSLPROGRAM 
				#pragma vertex vert
				#pragma fragment SpatialGTAO_X_frag
			ENDHLSL 
		}

		Pass 
		{ 
			Name"SpatialGTAO_Y"
			HLSLPROGRAM 
				#pragma vertex vert
				#pragma fragment SpatialGTAO_Y_frag
			ENDHLSL 
		}

		Pass 
		{ 
			Name"TemporalGTAO"
			HLSLPROGRAM 
				#pragma vertex vert
				#pragma fragment TemporalGTAO_frag
			ENDHLSL 
		}

		Pass 
		{ 
			Name"CombineGTAO"
			HLSLPROGRAM 
				#pragma vertex vert
				#pragma fragment CombienGTAO_frag
			ENDHLSL 
		}

		Pass 
		{ 
			Name"DeBugGTAO"
			HLSLPROGRAM 
				#pragma vertex vert
				#pragma fragment DeBugGTAO_frag
			ENDHLSL 
		}

		Pass 
		{ 
			Name"DeBugGTRO"
			HLSLPROGRAM 
				#pragma vertex vert
				#pragma fragment DeBugGTRO_frag
			ENDHLSL 
		}

		Pass 
		{ 
			Name"BentNormal"
			HLSLPROGRAM 
				#pragma vertex vert
				#pragma fragment DeBugBentNormal_frag
			ENDHLSL 
		}

	}
}

