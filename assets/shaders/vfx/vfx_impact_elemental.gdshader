shader_type spatial;
render_mode unshaded, cull_disabled;

uniform vec3 primary_color : source_color = vec3(1.0, 0.4, 0.0);
uniform vec3 secondary_color : source_color = vec3(1.0, 0.8, 0.2);
uniform float intensity : hint_range(0.5, 8.0) = 3.0;
uniform float dissolve_progress : hint_range(0.0, 1.0) = 0.0;
uniform float noise_scale : hint_range(0.5, 4.0) = 1.5;
uniform float distortion_amount : hint_range(0.0, 0.3) = 0.1;
uniform float soft_depth_fade : hint_range(0.01, 2.0) = 0.35;
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
	return mix(ramp, mix(primary_color, secondary_color, u), is_default_white);
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
	float radius = length(uv);

	float base_n = voronoi(UV + TIME * vec2(0.4, 0.25), noise_scale);
	float detail_n = voronoi(UV * 1.7 - TIME * vec2(0.2, 0.35), noise_scale * 1.8);
	float n = mix(base_n, detail_n, 0.45);

	vec2 flow = vec2(hash(UV + TIME * 0.33), hash(UV.yx - TIME * 0.21)) - 0.5;
	vec2 warped_uv = UV + flow * distortion_amount;
	float warped_n = voronoi(warped_uv + TIME * vec2(0.3, 0.2), noise_scale * 1.2);

	float shape = smoothstep(1.0, 0.0, radius);
	float heat = smoothstep(0.9, 0.15, warped_n);
	float edge = fresnel(2.4, NORMAL, VIEW);
	float ramp_t = clamp(heat + edge * 0.35, 0.0, 1.0);

	vec3 color = sample_gradient(ramp_t);
	float dissolve_noise = 1.0 - n;
	float dissolve_mask = smoothstep(dissolve_progress - 0.15, dissolve_progress + 0.15, dissolve_noise);

	float scene_depth = linear_depth(texture(depth_texture, SCREEN_UV).r, SCREEN_UV);
	float particle_depth = linear_depth(FRAGCOORD.z, SCREEN_UV);
	float soft_edge = clamp((scene_depth - particle_depth) / soft_depth_fade, 0.0, 1.0);

	float alpha = shape * dissolve_mask * soft_edge;
	ALPHA_SCISSOR_THRESHOLD = clamp(0.03 + dissolve_progress * 0.92, 0.0, 0.98);
	ALBEDO = color;
	EMISSION = color * intensity * (0.5 + heat + edge * 0.4);
	ALPHA = alpha * COLOR.a;
}
