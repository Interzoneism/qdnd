shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_opaque, diffuse_burley, specular_schlick_ggx;

uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;
uniform sampler2D DEPTH_TEXTURE : hint_depth_texture, filter_nearest;

uniform vec4 color_shallow : source_color = vec4(0.18, 0.56, 0.87, 1.0);
uniform vec4 color_deep : source_color = vec4(0.05, 0.20, 0.40, 1.0);
uniform float transparency : hint_range(0.0, 1.0, 0.01) = 0.55;
uniform float metallic : hint_range(0.0, 1.0, 0.01) = 0.02;
uniform float roughness : hint_range(0.0, 1.0, 0.01) = 0.18;
uniform float wave_height_scale : hint_range(0.0, 0.2, 0.001) = 0.01;
uniform float wave_speed : hint_range(0.0, 4.0, 0.01) = 1.0;
uniform float noise_scale : hint_range(0.2, 20.0, 0.01) = 3.2;
uniform float noise_speed : hint_range(0.0, 5.0, 0.01) = 0.4;
uniform float emission_strength : hint_range(0.0, 2.0, 0.01) = 0.12;
uniform float edge_softness : hint_range(0.01, 0.95, 0.01) = 0.22;
uniform vec4 border_color : source_color = vec4(1.0, 1.0, 1.0, 1.0);
uniform float border_scale : hint_range(0.0, 5.0, 0.01) = 1.4;
uniform float refraction_intensity : hint_range(0.0, 1.0, 0.01) = 0.3;

uniform float border_near = 0.05;
uniform float border_far = 4000.0;

varying vec3 world_pos;

float hash(vec2 p)
{
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float noise2(vec2 p)
{
    vec2 i = floor(p);
    vec2 f = fract(p);
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    vec2 u = f * f * (3.0 - 2.0 * f);
    return mix(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

float fbm(vec2 p)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    for (int i = 0; i < 5; i++)
    {
        sum += noise2(p * freq) * amp;
        freq *= 2.03;
        amp *= 0.5;
    }
    return sum;
}

float linear_depth(float raw_depth)
{
    return border_far * border_near / (border_near + raw_depth * (border_far - border_near));
}

vec3 normal_from_height(vec2 p)
{
    float e = 0.06;
    float hL = fbm(p - vec2(e, 0.0));
    float hR = fbm(p + vec2(e, 0.0));
    float hD = fbm(p - vec2(0.0, e));
    float hU = fbm(p + vec2(0.0, e));
    vec3 n = normalize(vec3(hL - hR, 1.8, hD - hU));
    return n;
}

void vertex()
{
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    vec2 wave_uv = world_pos.xz * (noise_scale * 0.35);

    float wave_a = fbm(wave_uv + vec2(TIME * wave_speed * 0.27, -TIME * wave_speed * 0.19));
    float wave_b = fbm(wave_uv * 1.87 + vec2(-TIME * wave_speed * 0.41, TIME * wave_speed * 0.31));
    float wave = ((wave_a * 0.64 + wave_b * 0.36) - 0.5) * 2.0;

    VERTEX.y += wave * wave_height_scale;
}

void fragment()
{
    vec2 centered = UV * 2.0 - vec2(1.0);
    float radial = length(centered);
    float edge = 1.0 - smoothstep(1.0 - edge_softness, 1.0, radial);
    if (edge <= 0.001)
    {
        discard;
    }

    vec2 flow_uv = world_pos.xz * noise_scale;
    float n_a = fbm(flow_uv + vec2(TIME * wave_speed * 0.21, TIME * wave_speed * 0.11));
    float n_b = fbm(flow_uv * 1.93 + vec2(-TIME * wave_speed * 0.34, TIME * wave_speed * 0.29));
    float n = clamp(n_a * 0.62 + n_b * 0.38, 0.0, 1.0);

    vec3 detail_normal = normal_from_height(flow_uv * 0.75 + vec2(TIME * wave_speed * 0.16));
    vec3 final_normal = normalize(mix(vec3(0.0, 1.0, 0.0), detail_normal, 0.72));
    NORMAL = final_normal;

    vec2 refract_uv = SCREEN_UV + final_normal.xz * (0.035 * refraction_intensity);
    refract_uv = clamp(refract_uv, vec2(0.001), vec2(0.999));

    float raw_scene_depth = texture(DEPTH_TEXTURE, SCREEN_UV).r;
    float raw_refract_depth = texture(DEPTH_TEXTURE, refract_uv).r;
    float raw_surface_depth = FRAGCOORD.z;

    float border_diff = max(0.0, linear_depth(raw_scene_depth) - linear_depth(raw_surface_depth));
    float depth_span = clamp((linear_depth(raw_refract_depth) - linear_depth(raw_surface_depth)) * 0.12, 0.0, 1.0);

    vec3 scene_color = texture(SCREEN_TEXTURE, refract_uv).rgb;
    vec3 depth_tint = mix(color_deep.rgb, color_shallow.rgb, clamp(depth_span + n * 0.22, 0.0, 1.0));
    vec3 refracted_mix = mix(depth_tint, scene_color, transparency);

    float foam_metric = border_diff * 10.0 + n * 0.2;
    float foam = 1.0 - smoothstep(border_scale, border_scale + 1.0, foam_metric);
    vec3 final_color = mix(refracted_mix, border_color.rgb, foam * border_color.a);

    ALBEDO = final_color;
    EMISSION = final_color * emission_strength * (0.65 + n * 0.45);
    METALLIC = metallic;
    ROUGHNESS = roughness;
    SPECULAR = 0.6;

    float alpha_from_transparency = mix(0.94, 0.28, transparency);
    ALPHA = clamp(alpha_from_transparency * edge * (0.84 + 0.16 * n), 0.02, 1.0);
}
