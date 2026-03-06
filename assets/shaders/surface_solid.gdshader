shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_opaque, diffuse_burley, specular_schlick_ggx;

uniform vec4 color_primary : source_color = vec4(1.0, 0.45, 0.08, 1.0);
uniform vec4 color_secondary : source_color = vec4(0.35, 0.08, 0.03, 1.0);
uniform vec4 edge_color : source_color = vec4(1.0, 0.82, 0.45, 1.0);
uniform float opacity : hint_range(0.0, 1.0, 0.01) = 0.6;
uniform float metallic : hint_range(0.0, 1.0, 0.01) = 0.02;
uniform float roughness : hint_range(0.0, 1.0, 0.01) = 0.45;
uniform float wave_height_scale : hint_range(0.0, 0.2, 0.001) = 0.008;
uniform float wave_speed : hint_range(0.0, 8.0, 0.01) = 1.8;
uniform float noise_scale : hint_range(0.1, 24.0, 0.01) = 5.0;
uniform float noise_speed : hint_range(0.0, 5.0, 0.01) = 0.9;
uniform float emission_strength : hint_range(0.0, 4.0, 0.01) = 0.45;
uniform float edge_softness : hint_range(0.01, 0.95, 0.01) = 0.22;
uniform float dissolve_strength : hint_range(0.0, 1.2, 0.01) = 0.35;

varying vec3 world_pos;

float hash(vec2 p)
{
    return fract(sin(dot(p, vec2(41.7, 197.3))) * 9182.417);
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
    for (int i = 0; i < 6; i++)
    {
        sum += noise2(p * freq) * amp;
        freq *= 2.17;
        amp *= 0.53;
    }
    return sum;
}

void vertex()
{
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;

    vec2 flow = world_pos.xz * (noise_scale * 0.28);
    float n1 = fbm(flow + vec2(TIME * wave_speed * 0.7, -TIME * wave_speed * 0.52));
    float n2 = fbm(flow * 2.3 - vec2(TIME * wave_speed * 0.44, TIME * wave_speed * 0.61));
    float wobble = (n1 * 0.55 + n2 * 0.45 - 0.5) * 2.0;

    VERTEX.y += wobble * wave_height_scale;
}

void fragment()
{
    vec2 centered = UV * 2.0 - vec2(1.0);
    float radial = length(centered);

    vec2 flow = world_pos.xz * noise_scale;
    float n0 = fbm(flow + vec2(TIME * noise_speed, -TIME * noise_speed * 0.71));
    float n1 = fbm(flow * 1.9 + vec2(-TIME * noise_speed * 0.63, TIME * noise_speed * 0.38));
    float n2 = fbm(flow * 3.6 - vec2(TIME * noise_speed * 0.42, TIME * noise_speed * 0.93));
    float chaos = clamp(n0 * 0.5 + n1 * 0.33 + n2 * 0.17, 0.0, 1.0);

    float dissolve = smoothstep(1.0 - edge_softness, 1.0, radial + (chaos - 0.5) * dissolve_strength);
    float alpha = clamp(opacity * (1.0 - dissolve), 0.0, 1.0);
    if (alpha <= 0.01)
    {
        discard;
    }

    vec3 body_color = mix(color_secondary.rgb, color_primary.rgb, clamp(chaos * 1.2, 0.0, 1.0));
    vec3 edge_mix = mix(body_color, edge_color.rgb, pow(1.0 - alpha, 0.8));

    ALBEDO = edge_mix;
    EMISSION = edge_mix * emission_strength * (0.65 + chaos * 0.75);
    METALLIC = metallic;
    ROUGHNESS = roughness;
    SPECULAR = 0.45;
    ALPHA = alpha;
}
