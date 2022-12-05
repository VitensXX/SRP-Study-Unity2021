#ifndef CUSTOM_BRDF_INCLUDED
#define CUSTOM_BRDF_INCLUDED

struct BRDF {
	float3 diffuse;
	float3 specular;
	float roughness;
};

//电介质的反射率平均约0.04
#define MIN_REFLECTIVITY 0.04

float OneMinusReflectivity (float metallic) {
	float range = 1.0 - MIN_REFLECTIVITY;
	return range - metallic * range;
}

BRDF GetBRDF (Surface surface) {
	BRDF brdf;
	float oneMinusReflectivity = OneMinusReflectivity(surface.metallic);
	brdf.diffuse = surface.color * oneMinusReflectivity;
	// brdf.specular = surface.color - brdf.diffuse;
	brdf.specular = lerp(MIN_REFLECTIVITY, surface.color, surface.metallic);
	brdf.roughness = 1.0;
	return brdf;
}

#endif