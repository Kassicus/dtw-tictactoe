shader_type canvas_item;

uniform sampler2D screen_texture : hint_screen_texture, repeat_disable, filter_linear_mipmap;
uniform float blur_amount : hint_range(0.0, 5.0) = 2.0;

void fragment() {
    vec2 pixel_size = 1.0 / vec2(textureSize(screen_texture, 0));
    vec4 color = vec4(0.0);

    // 9-tap Gaussian blur
    float weights[9] = float[](0.0162162162, 0.0540540541, 0.1216216216, 0.1945945946, 0.2270270270, 0.1945945946, 0.1216216216, 0.0540540541, 0.0162162162);

    for (int i = -4; i <= 4; i++) {
        for (int j = -4; j <= 4; j++) {
            vec2 offset = vec2(float(i), float(j)) * pixel_size * blur_amount;
            float weight = weights[i + 4] * weights[j + 4];
            color += texture(screen_texture, SCREEN_UV + offset) * weight;
        }
    }

    COLOR = color;
}
