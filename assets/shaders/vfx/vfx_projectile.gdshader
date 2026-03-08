shader_type spatial;
render_mode unshaded, cull_disabled;

uniform vec3 core_color : source_color = vec3(1.0, 0.5, 0.0);
uniform vec3 trail_color : source_color = vec3(1.0, 0.2, 0.0);
uniform float core_intensity : hint_range(1.0, 10.0) = 5.0;
uniform float trail_intensity : hint_range(0.5, 5.0) = 2.0;
uniform float trail_length : hint_range(0.2, 2.0) = 0.8;
uniform float noise_scale : hint_range(0.5, 3.0) = 1.0;
uniform float dissolve_progress : hint_range(0.0, 1.0) = 0.0;
uniform float soft_depth_fade : hint_range(0.01, 2.0) = 0.3;
uniform sampler2D gradient_ramp : source_color, filter_linear, repeat_disable;
uniform sampler2D depth_texture : hint_depth_texture, filter_linear, repeat_disable;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float voronoi(vec2 uv, float scale) {
	vec2 p = uv * scale;
	vec2 i = floor(p);
	vec2 f = fract(p);
	float min_dist = 1.0;
	for (int x = -1; x <= 1; x++) {
		for (int y = -1; y <= 1; y++) {
			vec2 neighbor = vec2(float(x), float(y));
			vec2 point = fract(sin(vec2(dot(i + neighbor, vec2(127.1, 311.7)), dot(i + neighbor, vec2(269.5, 183.3)))) * 43758.5453);
			float d = length(neighbor + point - f);
			min_dist = min(min_dist, d);
		}
	}
	return min_dist;
}

float fresnel(float power, vec3 normal, vec3 view) {
	return pow(1.0 - clamp(dot(normalize(normal), normalize(view)), 0.0, 1.0), power);
}

float linear_depth(float depth, vec2 suv) {
	// Projection matrix builtins unavailable in GpuParticles3D draw pass.
	// Raw depth is sufficient for relative soft-particle comparisons.
	return depth;
}

vec3 sample_gradient(float t) {
	float u = clamp(t, 0.0, 1.0);
	vec3 ramp = texture(gradient_ramp, vec2(u, 0.5)).rgb;
	float is_default_white = step(0.995, ramp.r) * step(0.995, ramp.g) * step(0.995, ramp.b);
	return mix(ramp, mix(core_color, trail_color, u), is_default_white);
}

void vertex() {
	mat4 billboard = mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	float angle = INSTANCE_CUSTOM.x * 6.28318530718;
	mat2 spin = mat2(vec2(cos(angle), sin(angle)), vec2(-sin(angle), cos(angle)));
	VERTEX.xy = spin * VERTEX.xy;
	MODELVIEW_MATRIX = VIEW_MATRIX * billboard;
	NORMAL = vec3(0.0, 0.0, 1.0);
}

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float core_dist = length(uv);

	float trail_axis = clamp((UV.y - (1.0 - trail_length)) / max(trail_length, 0.001), 0.0, 1.0);
	float trail_mask = (1.0 - trail_axis) * smoothstep(0.95, 0.1, abs(uv.x));

	float n = voronoi(UV + vec2(0.0, TIME * 1.5), noise_scale);
	float turbulence = smoothstep(0.15, 0.95, 1.0 - n);
	float core_mask = smoothstep(0.6, 0.0, core_dist);

	float ramp_t = clamp(trail_axis * 0.8 + (1.0 - core_mask) * 0.2, 0.0, 1.0);
	vec3 ramp_color = sample_gradient(ramp_t);
	vec3 core = mix(ramp_color, core_color, core_mask);
	vec3 trail = mix(trail_color, ramp_color, turbulence * 0.5);

	float dissolve_noise = 1.0 - n;
	float dissolve_mask = smoothstep(dissolve_progress - 0.1, dissolve_progress + 0.1, dissolve_noise);

	float scene_depth = linear_depth(texture(depth_texture, SCREEN_UV).r, SCREEN_UV);
	float particle_depth = linear_depth(FRAGCOORD.z, SCREEN_UV);
	float soft_edge = clamp((scene_depth - particle_depth) / soft_depth_fade, 0.0, 1.0);

	float alpha = clamp(core_mask + trail_mask * 0.9, 0.0, 1.0) * dissolve_mask * soft_edge;
	ALPHA_SCISSOR_THRESHOLD = clamp(0.02 + dissolve_progress * 0.9, 0.0, 0.96);
	ALBEDO = mix(trail, core, core_mask);
	EMISSION = core * core_intensity * core_mask + trail * trail_intensity * trail_mask * (0.6 + turbulence);
	ALPHA = alpha * COLOR.a;
}
