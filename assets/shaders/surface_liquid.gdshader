shader_type spatial;
render_mode blend_mix, cull_disabled, diffuse_burley, specular_schlick_ggx;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D DEPTH_TEXTURE : hint_depth_texture, filter_nearest;

uniform vec4 color_shallow : source_color = vec4(0.01, 0.2, 0.3, 1.0);
uniform vec4 color_deep : source_color = vec4(0.05, 0.15, 0.35, 1.0);

uniform float transparency : hint_range(0.0, 1.0, 0.01) = 0.6;
uniform float metallic : hint_range(0.0, 1.0, 0.01) = 0.0;
uniform float roughness : hint_range(0.0, 1.0, 0.01) = 0.15;

uniform sampler2D wave_texture_a;
uniform sampler2D wave_texture_b;
uniform vec2 wave_direction_a = vec2(-1.0, 0.0);
uniform vec2 wave_direction_b = vec2(0.0, 1.0);
uniform float wave_scale : hint_range(1.0, 30.0, 0.1) = 8.0;
uniform float wave_speed : hint_range(0.0, 1.0, 0.01) = 0.08;
uniform float wave_height : hint_range(0.0, 0.1, 0.001) = 0.003;

uniform sampler2D normal_texture_a;
uniform sampler2D normal_texture_b;
uniform vec2 normal_direction_a = vec2(-1.0, 0.2);
uniform vec2 normal_direction_b = vec2(0.2, 1.0);
uniform float normal_scale : hint_range(0.001, 1.0, 0.001) = 0.08;
uniform float normal_speed : hint_range(0.001, 0.5, 0.001) = 0.04;
uniform float normal_strength : hint_range(0.0, 1.0, 0.01) = 0.15;

uniform float max_visible_depth : hint_range(0.1, 10.0, 0.1) = 2.0;
uniform float edge_fade_distance : hint_range(0.0, 2.0, 0.01) = 0.3;
uniform vec3 surface_center = vec3(0.0);
uniform float surface_radius : hint_range(0.1, 200.0, 0.1) = 1.0;
uniform float edge_softness : hint_range(0.01, 0.95, 0.01) = 0.35;

uniform vec4 border_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float border_scale : hint_range(0.0, 5.0, 0.01) = 1.0;
uniform float border_near = 0.5;
uniform float border_far = 100.0;

uniform float refraction_intensity : hint_range(0.0, 0.5, 0.01) = 0.05;
uniform float emission_strength : hint_range(0.0, 2.0, 0.01) = 0.0;
uniform float tint_strength_min : hint_range(0.0, 1.0, 0.01) = 0.74;
uniform float tint_strength_max : hint_range(0.0, 1.0, 0.01) = 0.94;

varying vec3 world_pos;
varying vec2 world_uv;

float linear_depth(float raw, float near_plane, float far_plane)
{
    return far_plane * near_plane / (near_plane + raw * (far_plane - near_plane));
}

vec3 get_view_position_from_uv(vec2 uv, float depth)
{
    vec4 ndc = vec4(uv * 2.0 - 1.0, depth, 1.0);
    vec4 view = INV_PROJECTION_MATRIX * ndc;
    return view.xyz / view.w;
}

float sample_wave_height(vec2 uv_a, vec2 uv_b)
{
    float h1 = texture(wave_texture_a, uv_a).r;
    float h2 = texture(wave_texture_b, uv_b).r;
    return ((h1 + h2) * 0.5) * 2.0 - 1.0;
}

float hash_e(vec2 p)
{
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise_e(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    float a = hash_e(i);
    float b = hash_e(i + vec2(1.0, 0.0));
    float c = hash_e(i + vec2(0.0, 1.0));
    float d = hash_e(i + vec2(1.0, 1.0));
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

float fbm_e(vec2 p)
{
    float s = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++)
    {
        s += noise_e(p) * a;
        p *= 2.17;
        a *= 0.5;
    }
    return s;
}

void vertex()
{
    vec3 pre_displace_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_uv = pre_displace_world.xz;

    vec2 dir_a = normalize(wave_direction_a);
    vec2 dir_b = normalize(wave_direction_b);
    vec2 uv_a = world_uv / wave_scale + dir_a * TIME * wave_speed;
    vec2 uv_b = world_uv / wave_scale + dir_b * TIME * wave_speed;

    float wave = sample_wave_height(uv_a, uv_b);
    VERTEX.y += wave * wave_height;

    vec2 step_uv = vec2(0.05 / max(wave_scale, 0.001), 0.0);
    float h_l = sample_wave_height(uv_a - step_uv, uv_b - step_uv);
    float h_r = sample_wave_height(uv_a + step_uv, uv_b + step_uv);
    float h_d = sample_wave_height(uv_a - step_uv.yx, uv_b - step_uv.yx);
    float h_u = sample_wave_height(uv_a + step_uv.yx, uv_b + step_uv.yx);
    vec3 wave_normal = normalize(vec3(h_l - h_r, 0.5, h_d - h_u));
    NORMAL = wave_normal;

    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment()
{
    vec2 normal_dir_a = normalize(normal_direction_a);
    vec2 normal_dir_b = normalize(normal_direction_b);
    vec2 normal_world_uv = world_pos.xz;
    vec2 nt_a = normal_world_uv * normal_scale + normal_dir_a * TIME * normal_speed;
    vec2 nt_b = normal_world_uv * normal_scale + normal_dir_b * TIME * normal_speed;

    vec3 n1 = texture(normal_texture_a, nt_a).rgb;
    vec3 n2 = texture(normal_texture_b, nt_b).rgb;

    // Use tangent-space XY detail for refraction offset without mixing into interpolated view-space NORMAL.
    vec2 normal_offset = ((n1.xy - 0.5) + (n2.xy - 0.5)) * normal_strength;
    vec2 refract_uv = SCREEN_UV + normal_offset * refraction_intensity;
    refract_uv = clamp(refract_uv, vec2(0.001), vec2(0.999));

    float scene_depth_raw = texture(DEPTH_TEXTURE, SCREEN_UV).r;
    float refract_depth_raw = texture(DEPTH_TEXTURE, refract_uv).r;
    float surface_depth_raw = FRAGCOORD.z;

    float border_diff = max(
        0.0,
        linear_depth(scene_depth_raw, border_near, border_far) -
            linear_depth(surface_depth_raw, border_near, border_far));

    vec3 scene_view_pos = get_view_position_from_uv(refract_uv, refract_depth_raw);
    vec3 surface_view_pos = get_view_position_from_uv(SCREEN_UV, surface_depth_raw);
    float depth_distance = length(surface_view_pos - scene_view_pos);
    float depth_visibility = clamp(depth_distance / max(max_visible_depth, 0.001), 0.0, 1.0);
    depth_visibility = pow(depth_visibility, 0.75);

    vec3 screen_color = texture(SCREEN_TEXTURE, refract_uv).rgb;
    vec3 water_tint = mix(color_shallow.rgb, color_deep.rgb, clamp(depth_visibility * 0.95, 0.0, 1.0));
    float tint_strength = mix(tint_strength_min, tint_strength_max, depth_visibility);
    vec3 blended = mix(screen_color, water_tint, clamp(tint_strength, 0.0, 1.0));

    float foam = 0.0;
    if (border_scale > 0.0)
    {
        float near_band = smoothstep(0.004, 0.018, border_diff);
        float far_band = 1.0 - smoothstep(0.018, max(0.03, border_scale * 0.13), border_diff);
        foam = near_band * far_band;
    }

    vec3 foam_tint = mix(water_tint, border_color.rgb, 0.45);
    vec3 final_color = mix(blended, foam_tint, foam * 0.35);

    // World-space radial edge fading (always works, gives smooth organic edges).
    float radial = length(world_pos.xz - surface_center.xz) / max(surface_radius, 0.1);
    float edge_noise = (fbm_e(world_pos.xz * 3.5 + TIME * 0.03) - 0.5) * 0.35;
    float radial_edge = 1.0 - smoothstep(1.0 - edge_softness, 1.0, radial + edge_noise);

    // Depth-based edge fade (bonus when ground geometry exists).
    float depth_edge = edge_fade_distance > 0.0
        ? mix(0.72, 1.0, clamp(border_diff / edge_fade_distance, 0.0, 1.0))
        : 1.0;

    // Noise-based alpha variation to break up cell grid patterns.
    float grid_break = fbm_e(world_pos.xz * 1.9 + TIME * 0.015) * 0.1 + 0.9;

    float combined_edge = radial_edge * depth_edge * grid_break;

    ALBEDO = final_color;
    EMISSION = final_color * emission_strength;
    METALLIC = metallic;
    ROUGHNESS = roughness;
    SPECULAR = 0.6;
    ALPHA = clamp(combined_edge * transparency * COLOR.a, 0.0, 1.0);
}
