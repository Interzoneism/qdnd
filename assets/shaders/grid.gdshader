shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, unshaded;

uniform vec4 grid_color : source_color = vec4(1.0, 1.0, 1.0, 0.1);
uniform vec4 accent_color : source_color = vec4(0.7, 0.6, 0.4, 0.2);
uniform float grid_size = 1.0;
uniform float line_width = 0.02;
uniform float accent_every = 5.0;
uniform float fade_distance = 15.0;

void fragment() {
    vec2 world_pos = (INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xz;
    vec2 grid_pos = world_pos / grid_size;
    
    vec2 grid_frac = abs(fract(grid_pos - 0.5) - 0.5);
    float line = min(grid_frac.x, grid_frac.y);
    float grid_line = 1.0 - smoothstep(0.0, line_width, line);
    
    // Accent lines every N cells
    vec2 accent_frac = abs(fract(grid_pos / accent_every - 0.5) - 0.5);
    float accent_line = min(accent_frac.x, accent_frac.y);
    float accent = 1.0 - smoothstep(0.0, line_width * 1.5, accent_line);
    
    // Distance fade
    float dist = length(world_pos);
    float fade = 1.0 - smoothstep(fade_distance * 0.5, fade_distance, dist);
    
    vec4 color = mix(grid_color, accent_color, accent);
    ALBEDO = color.rgb;
    ALPHA = max(grid_line, accent) * color.a * fade;
}
