shader_type spatial;
render_mode unshaded, cull_disabled;

uniform vec3 primary_color : source_color = vec3(1.0, 0.5, 0.0);
uniform vec3 secondary_color : source_color = vec3(1.0, 0.8, 0.3);
uniform float ring_progress : hint_range(0.0, 1.0) = 0.0;
uniform float ring_width : hint_range(0.02, 0.3) = 0.1;
uniform float intensity : hint_range(0.5, 8.0) = 4.0;
uniform float noise_scale : hint_range(0.5, 3.0) = 1.5;
uniform float distortion_amount : hint_range(0.0, 0.2) = 0.05;
uniform float dissolve_progress : hint_range(0.0, 1.0) = 0.0;
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
	vec4 ndc = vec4(suv * 2.0 - 1.0, depth, 1.0);
	vec4 view_pos = INV_PROJECTION_MATRIX * ndc;
	return -view_pos.z / max(view_pos.w, 0.0001);
}

vec3 sample_gradient(float t) {
	float u = clamp(t, 0.0, 1.0);
	vec3 ramp = texture(gradient_ramp, vec2(u, 0.5)).rgb;
	float is_default_white = step(0.995, ramp.r) * step(0.995, ramp.g) * step(0.995, ramp.b);
	return mix(ramp, mix(primary_color, secondary_color, u), is_default_white);
}

void vertex() {
	// Billboard: face camera so the ring shape in fragment() renders correctly
	mat4 billboard = mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = VIEW_MATRIX * billboard;
	NORMAL = vec3(0.0, 0.0, 1.0);
}

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float dist = length(uv);

	vec2 distortion = (vec2(hash(UV * 2.1 + TIME * 0.6), hash(UV.yx * 2.1 - TIME * 0.4)) - 0.5) * distortion_amount;
	float n = voronoi(UV + distortion + TIME * vec2(0.2, 0.1), noise_scale);
	float wave = 1.0 - smoothstep(0.0, 1.0, abs(dist - ring_progress) / max(ring_width, 0.001));
	float ragged = smoothstep(0.1, 0.95, 1.0 - n);
	float ring = clamp(wave * (0.65 + ragged * 0.5), 0.0, 1.0);

	float edge_fade = 1.0 - smoothstep(0.95, 1.15, dist);
	float dissolve_noise = 1.0 - n;
	float dissolve_mask = smoothstep(dissolve_progress - 0.1, dissolve_progress + 0.1, dissolve_noise);

	float ramp_t = clamp(ring_progress + ragged * 0.3, 0.0, 1.0);
	vec3 color = sample_gradient(ramp_t);

	float scene_depth = linear_depth(texture(depth_texture, SCREEN_UV).r, SCREEN_UV);
	float particle_depth = linear_depth(FRAGCOORD.z, SCREEN_UV);
	float soft_edge = clamp((scene_depth - particle_depth) / soft_depth_fade, 0.0, 1.0);

	ALPHA_SCISSOR_THRESHOLD = clamp(0.02 + dissolve_progress * 0.9, 0.0, 0.95);
	ALBEDO = color;
	EMISSION = color * intensity * (ring + ragged * 0.4);
	ALPHA = ring * edge_fade * dissolve_mask * soft_edge * COLOR.a;
}
