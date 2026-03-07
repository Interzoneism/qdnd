shader_type spatial;
render_mode unshaded, cull_disabled;

uniform vec3 primary_color : source_color = vec3(0.4, 0.6, 1.0);
uniform vec3 secondary_color : source_color = vec3(0.8, 0.9, 1.0);
uniform float column_height : hint_range(0.5, 3.0) = 1.5;
uniform float intensity : hint_range(0.5, 8.0) = 2.5;
uniform float scroll_speed : hint_range(0.5, 4.0) = 1.5;
uniform float dissolve_progress : hint_range(0.0, 1.0) = 0.0;
uniform float noise_scale : hint_range(0.5, 4.0) = 1.3;
uniform float fresnel_power : hint_range(0.5, 5.0) = 2.2;
uniform float soft_depth_fade : hint_range(0.01, 2.0) = 0.45;
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
	// Spin on XZ (horizontal plane) so column particles distribute around the caster
	VERTEX.xz = spin * VERTEX.xz;
	VERTEX.y *= column_height;
	MODELVIEW_MATRIX = VIEW_MATRIX * billboard;
	NORMAL = vec3(0.0, 0.0, 1.0);
}

void fragment() {
	vec2 uv = UV;
	float radial = abs(uv.x * 2.0 - 1.0);
	float vertical = uv.y;

	vec2 flow_uv = vec2(uv.x * 1.25 + sin((uv.y + TIME) * 7.0) * 0.03, uv.y + TIME * scroll_speed);
	float stream_a = voronoi(flow_uv, noise_scale);
	float stream_b = voronoi(flow_uv * 1.8 - vec2(0.0, TIME * scroll_speed * 0.65), noise_scale * 1.7);
	float stream = smoothstep(0.2, 0.95, 1.0 - mix(stream_a, stream_b, 0.5));

	float column_mask = smoothstep(0.9, 0.1, radial);
	float tip_soften = smoothstep(0.0, 0.25, vertical) * (1.0 - smoothstep(0.82, 1.0, vertical));
	float f = fresnel(fresnel_power, NORMAL, VIEW);

	float ramp_t = clamp(stream * 0.7 + (1.0 - radial) * 0.25 + f * 0.35, 0.0, 1.0);
	vec3 color = sample_gradient(ramp_t);

	float dissolve_noise = 1.0 - stream_a;
	float dissolve_mask = smoothstep(dissolve_progress - 0.1, dissolve_progress + 0.1, dissolve_noise);

	float scene_depth = linear_depth(texture(depth_texture, SCREEN_UV).r, SCREEN_UV);
	float particle_depth = linear_depth(FRAGCOORD.z, SCREEN_UV);
	float soft_edge = clamp((scene_depth - particle_depth) / soft_depth_fade, 0.0, 1.0);

	float alpha = column_mask * tip_soften * dissolve_mask * soft_edge;
	ALPHA_SCISSOR_THRESHOLD = clamp(0.03 + dissolve_progress * 0.9, 0.0, 0.95);
	ALBEDO = color;
	EMISSION = color * intensity * (0.6 + stream + f * 0.8);
	ALPHA = alpha * COLOR.a;
}
