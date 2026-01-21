shader_type spatial;
render_mode blend_mix, depth_prepass_alpha, cull_disabled, specular_schlick_ggx;

// Base color and transparency
uniform vec4 base_color : source_color = vec4(1.0, 0.3, 0.3, 0.5);
uniform float roughness : hint_range(0.0, 1.0) = 0.05;
uniform float specular_strength : hint_range(0.0, 1.0) = 0.8;

// Fresnel/rim for jelly look
uniform float rim_power : hint_range(0.0, 10.0) = 2.0;
uniform float rim_intensity : hint_range(0.0, 1.0) = 0.6;

// Inner glow for jelly depth
uniform vec4 inner_color : source_color = vec4(1.0, 0.5, 0.5, 1.0);
uniform float inner_glow : hint_range(0.0, 1.0) = 0.5;

// Deformation parameters (controlled by script)
uniform float squash_amount : hint_range(-1.0, 1.0) = 0.0;
uniform vec3 deform_origin = vec3(0.0, 0.0, 0.0);

// Wobble parameters
uniform float wobble_speed : hint_range(0.0, 50.0) = 12.0;
uniform float wobble_amount : hint_range(0.0, 1.0) = 0.0;
uniform float wobble_time = 0.0;

void vertex() {
    vec3 local_pos = VERTEX - deform_origin;

    // Height factor for deformation falloff
    float height_factor = clamp((local_pos.y + 0.5) / 1.0, 0.0, 1.0);

    // More dramatic squash and stretch
    float vertical_scale = 1.0 - squash_amount * 0.7;
    float horizontal_scale = 1.0 + squash_amount * 0.5;

    // Multi-frequency wobble for organic jelly motion
    float wobble1 = sin(wobble_time * wobble_speed + local_pos.x * 8.0 + local_pos.y * 4.0);
    float wobble2 = cos(wobble_time * wobble_speed * 1.3 + local_pos.z * 6.0);
    float wobble3 = sin(wobble_time * wobble_speed * 0.7 + local_pos.x * 3.0 - local_pos.z * 5.0);
    float combined_wobble = (wobble1 * 0.5 + wobble2 * 0.3 + wobble3 * 0.2) * wobble_amount;

    // Apply squash (more at bottom)
    float squash_falloff = 1.0 - height_factor * 0.3;
    VERTEX.y = (VERTEX.y - deform_origin.y) * vertical_scale + deform_origin.y;
    VERTEX.x = (VERTEX.x - deform_origin.x) * (horizontal_scale + combined_wobble * squash_falloff) + deform_origin.x;
    VERTEX.z = (VERTEX.z - deform_origin.z) * (horizontal_scale + combined_wobble * 0.7 * squash_falloff) + deform_origin.z;

    // Add vertical wobble too
    VERTEX.y += combined_wobble * 0.3 * wobble_amount * height_factor;

    NORMAL = normalize(NORMAL * vec3(1.0 / horizontal_scale, 1.0 / vertical_scale, 1.0 / horizontal_scale));
}

void fragment() {
    float fresnel = pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), rim_power);

    // Core color darker/more saturated, edges brighter
    vec3 core_color = inner_color.rgb * 0.8;
    vec3 edge_color = base_color.rgb + vec3(0.3);

    vec3 color = mix(core_color, edge_color, fresnel * rim_intensity);

    // Add bright rim highlight
    color += vec3(1.0) * fresnel * 0.4;

    ALBEDO = color;

    // More transparent in center, more opaque at edges (like real jelly)
    float center_alpha = base_color.a * 0.4;
    float edge_alpha = min(base_color.a + 0.4, 0.95);
    ALPHA = mix(center_alpha, edge_alpha, fresnel);

    ROUGHNESS = roughness;
    METALLIC = 0.0;
    SPECULAR = specular_strength;

    // Subtle inner glow emission
    EMISSION = inner_color.rgb * inner_glow * 0.15 * (1.0 - fresnel);
}
