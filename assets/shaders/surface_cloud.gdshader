shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_opaque, diffuse_burley, specular_schlick_ggx;

uniform vec4 color_primary : source_color = vec4(0.85, 0.85, 0.85, 1.0);
uniform vec4 color_secondary : source_color = vec4(0.52, 0.56, 0.6, 1.0);
uniform float opacity : hint_range(0.0, 1.0, 0.01) = 0.42;
uniform float wave_height_scale : hint_range(0.0, 0.2, 0.001) = 0.018;
uniform float wave_speed : hint_range(0.0, 3.0, 0.01) = 0.45;
uniform float noise_scale : hint_range(0.1, 12.0, 0.01) = 2.0;
uniform float noise_speed : hint_range(0.0, 2.0, 0.01) = 0.22;
uniform float cloud_density : hint_range(0.0, 2.0, 0.01) = 1.0;
uniform float edge_softness : hint_range(0.01, 0.95, 0.01) = 0.35;
uniform float emission_strength : hint_range(0.0, 2.0, 0.01) = 0.08;
uniform float height_fade : hint_range(0.0, 4.0, 0.01) = 1.25;

varying vec3 world_pos;
varying vec3 local_pos;

float hash(vec2 p)
{
    return fract(sin(dot(p, vec2(73.1, 271.9))) * 15317.1327);
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
        freq *= 2.04;
        amp *= 0.52;
    }
    return sum;
}

void vertex()
{
    local_pos = VERTEX;
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;

    vec2 sway_uv = world_pos.xz * (noise_scale * 0.4);
    float sway_a = fbm(sway_uv + vec2(TIME * wave_speed * 0.2, TIME * wave_speed * 0.13));
    float sway_b = fbm(sway_uv * 1.8 - vec2(TIME * wave_speed * 0.17, TIME * wave_speed * 0.24));
    float sway = (sway_a * 0.6 + sway_b * 0.4 - 0.5) * 2.0;

    VERTEX.y += sway * wave_height_scale;
}

void fragment()
{
    vec2 world_uv = world_pos.xz * noise_scale;
    float t = TIME * noise_speed;

    float n0 = fbm(world_uv + vec2(t * 0.25, t * 0.17));
    float n1 = fbm(world_uv * 1.85 - vec2(t * 0.13, t * 0.21));
    float n2 = fbm(world_uv * 3.2 + vec2(-t * 0.09, t * 0.05));
    float billow = clamp(n0 * 0.5 + n1 * 0.35 + n2 * 0.15, 0.0, 1.0);

    vec2 centered = UV * 2.0 - vec2(1.0);
    float radial = length(centered);
    float edge = 1.0 - smoothstep(1.0 - edge_softness, 1.0, radial + (billow - 0.5) * 0.16);

    float h = clamp(local_pos.y * 3.9 + 0.5, 0.0, 1.0);
    float height_mask = 1.0 - smoothstep(0.0, height_fade, h);

    float density = smoothstep(0.25, 0.95, billow) * edge * mix(0.55, 1.0, height_mask);
    float alpha = clamp(opacity * density * cloud_density, 0.0, 1.0);
    if (alpha <= 0.01)
    {
        discard;
    }

    vec3 body = mix(color_secondary.rgb, color_primary.rgb, clamp(0.25 + billow * 0.95, 0.0, 1.0));

    ALBEDO = body;
    EMISSION = body * emission_strength * (0.5 + billow * 0.35);
    METALLIC = 0.0;
    ROUGHNESS = 0.86;
    SPECULAR = 0.1;
    ALPHA = alpha;
}
