shader_type spatial;
render_mode unshaded, cull_disabled;

uniform vec3 primary_color : source_color = vec3(0.7, 0.35, 1.0);
uniform vec3 secondary_color : source_color = vec3(0.95, 0.85, 1.0);
uniform float intensity : hint_range(0.5, 8.0) = 2.8;
uniform float dissolve_progress : hint_range(0.0, 1.0) = 0.0;
uniform float noise_scale : hint_range(0.5, 4.0) = 1.2;
uniform float fresnel_power : hint_range(0.5, 5.0) = 2.0;
uniform float pulse_speed : hint_range(0.0, 4.0) = 2.0;
uniform float distortion_amount : hint_range(0.0, 0.25) = 0.08;
uniform float soft_depth_fade : hint_range(0.01, 2.0) = 0.4;
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
	mat4 billboard = mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	float angle = INSTANCE_CUSTOM.x * 6.28318530718;
	mat2 spin = mat2(cos(angle), -sin(angle), sin(angle), cos(angle));
	VERTEX.xy = spin * VERTEX.xy;
	MODELVIEW_MATRIX = VIEW_MATRIX * billboard;
	NORMAL = vec3(0.0, 0.0, 1.0);
}

void fragment() {
	vec2 uv = UV * 2.0 - 1.0;
	float radius = length(uv);

	vec2 drift = vec2(hash(UV * 1.3 + TIME * 0.14), hash(UV.yx * 1.3 - TIME * 0.17)) - 0.5;
	vec2 warped_uv = UV + drift * distortion_amount;
	float n_a = voronoi(warped_uv + TIME * vec2(0.12, 0.08), noise_scale);
	float n_b = voronoi(warped_uv * 1.6 - TIME * vec2(0.09, 0.11), noise_scale * 1.6);
	float ethereal = smoothstep(0.1, 0.95, 1.0 - mix(n_a, n_b, 0.55));

	float center_mask = smoothstep(0.85, 0.05, radius);
	float rim = smoothstep(0.3, 1.0, radius) * (1.0 - smoothstep(0.85, 1.05, radius));
	float f = fresnel(fresnel_power, NORMAL, VIEW);
	float pulse = 0.65 + 0.35 * sin(TIME * pulse_speed + radius * 8.0);

	float ramp_t = clamp(ethereal * 0.6 + rim * 0.35 + f * 0.4, 0.0, 1.0);
	vec3 color = sample_gradient(ramp_t);

	float dissolve_noise = 1.0 - n_a;
	float dissolve_mask = smoothstep(dissolve_progress - 0.12, dissolve_progress + 0.12, dissolve_noise);

	float scene_depth = linear_depth(texture(depth_texture, SCREEN_UV).r, SCREEN_UV);
	float particle_depth = linear_depth(FRAGCOORD.z, SCREEN_UV);
	float soft_edge = clamp((scene_depth - particle_depth) / soft_depth_fade, 0.0, 1.0);

	float alpha = (center_mask * 0.35 + rim * 0.9 + f * 0.6) * dissolve_mask * soft_edge;
	ALPHA_SCISSOR_THRESHOLD = clamp(0.02 + dissolve_progress * 0.9, 0.0, 0.95);
	ALBEDO = color;
	EMISSION = color * intensity * pulse * (0.8 + rim * 1.2 + f);
	ALPHA = alpha * COLOR.a;
}
