shader_type fog;

uniform vec3 fog_color : source_color = vec3(0.8, 0.85, 0.9);
uniform float fog_density : hint_range(0.0, 2.0) = 0.3;
uniform float noise_scale = 2.0;
uniform float noise_speed = 0.15;
uniform float edge_fade : hint_range(0.0, 1.0) = 0.3;
uniform float height_fade : hint_range(0.0, 5.0) = 1.0;

float hash3(vec3 p)
{
    p = fract(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return fract((p.x + p.y) * p.z);
}

float noise3(vec3 p)
{
    vec3 i = floor(p);
    vec3 f = fract(p);

    float n000 = hash3(i + vec3(0.0, 0.0, 0.0));
    float n100 = hash3(i + vec3(1.0, 0.0, 0.0));
    float n010 = hash3(i + vec3(0.0, 1.0, 0.0));
    float n110 = hash3(i + vec3(1.0, 1.0, 0.0));
    float n001 = hash3(i + vec3(0.0, 0.0, 1.0));
    float n101 = hash3(i + vec3(1.0, 0.0, 1.0));
    float n011 = hash3(i + vec3(0.0, 1.0, 1.0));
    float n111 = hash3(i + vec3(1.0, 1.0, 1.0));

    vec3 u = f * f * (3.0 - 2.0 * f);

    float nx00 = mix(n000, n100, u.x);
    float nx10 = mix(n010, n110, u.x);
    float nx01 = mix(n001, n101, u.x);
    float nx11 = mix(n011, n111, u.x);

    float nxy0 = mix(nx00, nx10, u.y);
    float nxy1 = mix(nx01, nx11, u.y);

    return mix(nxy0, nxy1, u.z);
}

float fbm3(vec3 p)
{
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    for (int i = 0; i < 4; i++)
    {
        sum += noise3(p * freq) * amp;
        freq *= 2.02;
        amp *= 0.5;
    }
    return sum;
}

void fog()
{
    vec3 world_pos = WORLD_POSITION;
    vec3 drift = vec3(TIME * noise_speed, 0.0, TIME * noise_speed * 0.7);
    float n = fbm3(world_pos * noise_scale + drift);

    vec3 local = UVW * 2.0 - 1.0;
    float edge = 1.0 - smoothstep(1.0 - edge_fade, 1.0, length(local.xz));
    float height = 1.0 - smoothstep(0.0, height_fade, local.y + 0.5);

    DENSITY = fog_density * n * edge * height;
    ALBEDO = fog_color;
    EMISSION = vec3(0.0);
}
