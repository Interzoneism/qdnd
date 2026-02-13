shader_type spatial;
render_mode unshaded, cull_front, skip_vertex_transform;

// Outline / glow overlay shader for combatant hover & selection.
// Applied as a next-pass material on character meshes.
// Renders a slightly inflated, back-face-only shell in the chosen color.

uniform vec4 outline_color : source_color = vec4(1.0, 1.0, 1.0, 0.7);
uniform float outline_thickness : hint_range(0.0, 0.15) = 0.035;
uniform float pulse_speed : hint_range(0.0, 8.0) = 2.0;
uniform float pulse_amount : hint_range(0.0, 0.5) = 0.15;
uniform float fresnel_power : hint_range(0.5, 5.0) = 2.0;

void vertex() {
    // Transform vertex and normal to view space
    vec4 view_pos = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
    vec3 view_normal = normalize((MODELVIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);

    // Pulse the outline thickness over time
    float pulse = 1.0 + pulse_amount * sin(TIME * pulse_speed);
    float thickness = outline_thickness * pulse;

    // Inflate along normal in view space
    view_pos.xyz += view_normal * thickness;

    VERTEX = view_pos.xyz;
    POSITION = PROJECTION_MATRIX * view_pos;
    NORMAL = view_normal;
    UV = UV;
}

void fragment() {
    // Compute fresnel for softer edge falloff
    float fresnel = pow(1.0 - abs(dot(NORMAL, VIEW)), fresnel_power);
    float alpha = outline_color.a * mix(0.4, 1.0, fresnel);

    ALBEDO = outline_color.rgb;
    ALPHA = alpha;
}
