shader_type spatial;

// Ocean parameters
uniform vec4 shallow_color : source_color = vec4(0.2, 0.5, 0.6, 0.9);
uniform vec4 deep_color : source_color = vec4(0.05, 0.15, 0.25, 1.0);
uniform vec4 foam_color : source_color = vec4(0.9, 0.95, 1.0, 0.8);

uniform float wave_speed : hint_range(0.0, 2.0) = 0.5;
uniform float wave_height : hint_range(0.0, 2.0) = 0.3;
uniform float wave_frequency : hint_range(0.0, 10.0) = 2.0;

uniform float foam_amount : hint_range(0.0, 1.0) = 0.3;
uniform float fresnel_power : hint_range(0.0, 10.0) = 3.0;

uniform sampler2D noise_texture : hint_default_white;
uniform float noise_scale : hint_range(0.01, 1.0) = 0.1;

varying vec3 world_vertex;
varying float wave_factor;

void vertex() {
    world_vertex = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;

    // Multi-layered wave displacement
    float time = TIME * wave_speed;
    vec2 uv_offset = world_vertex.xz * noise_scale;

    // Primary wave
    float wave1 = sin(world_vertex.x * wave_frequency + time * 1.5) *
                  cos(world_vertex.z * wave_frequency * 0.7 + time);

    // Secondary wave (cross pattern)
    float wave2 = sin(world_vertex.x * wave_frequency * 0.5 - time) *
                  cos(world_vertex.z * wave_frequency * 1.2 + time * 0.8);

    // Small ripples from noise
    float noise_val = texture(noise_texture, uv_offset + vec2(time * 0.1)).r;
    float ripple = (noise_val - 0.5) * 0.5;

    // Combine waves
    wave_factor = (wave1 * 0.6 + wave2 * 0.3 + ripple * 0.1);
    VERTEX.y += wave_factor * wave_height;

    // Update normal for proper lighting
    vec3 tangent_x = vec3(1.0, wave_frequency * wave_height * cos(world_vertex.x * wave_frequency + time * 1.5), 0.0);
    vec3 tangent_z = vec3(0.0, wave_frequency * wave_height * cos(world_vertex.z * wave_frequency * 0.7 + time), 1.0);
    NORMAL = normalize(cross(tangent_z, tangent_x));
}

void fragment() {
    // Fresnel effect for edge highlighting
    float fresnel = pow(1.0 - dot(NORMAL, VIEW), fresnel_power);

    // Depth-based color mixing
    float depth_factor = clamp(wave_factor * 2.0 + 0.5, 0.0, 1.0);
    vec3 water_color = mix(deep_color.rgb, shallow_color.rgb, depth_factor);

    // Add foam at wave peaks
    float foam = smoothstep(0.3, 0.6, wave_factor + foam_amount);
    water_color = mix(water_color, foam_color.rgb, foam * foam_amount);

    // Fresnel adds sky reflection simulation
    vec3 sky_color = vec3(0.6, 0.7, 0.9);
    water_color = mix(water_color, sky_color, fresnel * 0.5);

    ALBEDO = water_color;
    METALLIC = 0.1;
    ROUGHNESS = 0.1 + foam * 0.4;
    SPECULAR = 0.8;

    // Transparency at edges
    ALPHA = mix(shallow_color.a, 1.0, fresnel);
}
