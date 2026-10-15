shader_type canvas_item;

// ════════════════════════════════════════════════════════════════════
// 永世沙漏 · 卡牌框（双色 · 对角分割）
//
//   · 分割线：**过卡框中点**，标准坐标系 −30°（y 向上、逆时针为正）
//   · 线上侧 = color_top（深蓝）／线下侧 = color_bottom（琥珀金）
//   · 分割处 ±blend_px 融色
//   · 卡框中部（文字面板）还原原图本色 ⇒ 卡面文字照常可读
//   · 正中预留图标接口（icon_tex / icon_center / icon_size_px / icon_alpha）
//
// ⚠️ 关键：卡框贴图是 **ui_atlas 的切片**（AtlasTexture），UV 是「图集坐标」不是 0~1，
// 所以必须先按 uv_rect（图集像素区域）把它归一化回「卡框本地坐标」再画分割线。
// 本体的三张切片都在 ui_atlas_0.png（2048×2048，598×844）：
//   attack = (1320, 83)   skill = (1221, 929)   power = (621, 929)
//
// 原贴图只用来提供「亮度 + 透明度」—— 本体卡框是红底图（均色约 #8F2D32），
// 各职业靠 hsv.gdshader 换色；这里改成「用原图明暗 × 我们的目标色」。
// ════════════════════════════════════════════════════════════════════

/// 卡框在**图集里的像素区域**（x, y, w, h）。每种卡框一张材质，只差这个值。
uniform vec4 uv_rect = vec4(1320.0, 83.0, 598.0, 844.0);

/// **线上侧**颜色（深蓝 #1E3F6B）。alpha = 上色强度（0 = 完全保留原图）。
uniform vec4 color_top : source_color = vec4(0.118, 0.247, 0.420, 1.0);

/// **线下侧**颜色（琥珀金 #D0952F）。alpha = 上色强度。
uniform vec4 color_bottom : source_color = vec4(0.816, 0.584, 0.184, 1.0);

/// 分割线角度（**标准坐标系**：y 向上、逆时针为正。屏幕 y 向下，故取 −sin）。
uniform float split_angle_deg : hint_range(-90.0, 90.0) = -30.0;

/// 分割线融色半宽（±，单位 = 卡框像素；卡框本体 598×844）
uniform float blend_px : hint_range(0.0, 200.0) = 50.0;

/// 原图平均亮度基准（用来把明暗还原成 1.0 附近）
uniform float shade_ref : hint_range(0.01, 1.0) = 0.30;

/// 明暗保留强度：0 = 完全平涂，1 = 完全沿用原图明暗
uniform float shade_strength : hint_range(0.0, 1.0) = 0.75;

/// 整体提亮系数
uniform float gain : hint_range(0.5, 2.0) = 1.05;

/// 上色是否"平涂"：1 = 不吃原图明暗，直接用纯色叠上去（黑底框用）。
uniform float tint_flat : hint_range(0.0, 1.0) = 0.0;

// ── 文字面板（卡框中部：保留原图本色）────────────────────────────────
// 实测（ui_atlas_0.png 攻击切片）：面板在卡框本地 x 47..547、y ≈390..786
// （y 390..464 是肖像窗底部那道 V 形装饰，颜色已经是面板灰 ⇒ 一起还原）。
/// 面板区域（卡框本地像素：x, y, w, h）
uniform vec4 panel_rect = vec4(47.0, 390.0, 500.0, 396.0);

/// 面板圆角半径（像素）
uniform float panel_radius : hint_range(0.0, 80.0) = 12.0;

/// 面板边缘羽化（像素）
uniform float panel_feather : hint_range(0.0, 20.0) = 2.0;

// ── 正中图标（接口预留；默认 icon_alpha = 0 即不显示）──────────────────
/// 图标贴图
uniform sampler2D icon_tex : hint_default_white, filter_linear, repeat_disable;

/// 图标中心（卡框本地像素）
uniform vec2 icon_center = vec2(299.0, 620.0);

/// 图标边长（像素，正方）
uniform float icon_size_px : hint_range(0.0, 600.0) = 220.0;

/// 图标不透明度（1 = 实心；水印调到 0.3 左右）
uniform float icon_alpha : hint_range(0.0, 1.0) = 0.0;

varying vec4 modulate_color;

void vertex() {
    modulate_color = COLOR;
}

/// 圆角矩形有符号距离（p 为相对中心的坐标）
float rounded_rect_sdf(vec2 p, vec2 half_size, float r) {
    vec2 q = abs(p) - half_size + r;
    return min(max(q.x, q.y), 0.0) + length(max(q, vec2(0.0))) - r;
}

void fragment() {
    vec4 src = texture(TEXTURE, UV);

    // 图集坐标 → 卡框本地像素坐标（0..w, 0..h）
    vec2 atlas_px = 1.0 / max(TEXTURE_PIXEL_SIZE, vec2(1e-6));
    vec2 local_px = UV * atlas_px - uv_rect.xy;
    vec2 size = uv_rect.zw;

    // 过中点的分割线：标准坐标系角度 a
    //   标准方向 = (cos a, sin a)  ⇒  屏幕方向（y 向下）= (cos a, −sin a)
    //   法线取「指向上侧」那一支
    float a = radians(split_angle_deg);
    vec2 dir = vec2(cos(a), -sin(a));
    vec2 nrm = vec2(dir.y, -dir.x);
    float sd = dot(local_px - size * 0.5, nrm);
    float m = smoothstep(-blend_px, blend_px, sd);   // 1 = 上侧

    vec3 base = mix(color_bottom.rgb, color_top.rgb, m);
    float tint = mix(color_bottom.a, color_top.a, m);

    // 原图亮度 → 明暗（保留描边 / 倒角 / 立体感）
    float y = dot(src.rgb, vec3(0.2989, 0.5870, 0.1140));
    float k = mix(1.0, clamp(y / shade_ref, 0.15, 2.0), shade_strength) * gain;
    vec3 tinted = mix(src.rgb, base * mix(k, gain, tint_flat), tint);

    // ① 双色（框体）
    vec3 col = tinted;

    // ② 文字面板：还原成原图本色（原本的深灰），不再上双色
    vec2 panel_half = panel_rect.zw * 0.5;
    vec2 panel_center = panel_rect.xy + panel_half;
    float d_panel = rounded_rect_sdf(local_px - panel_center, panel_half, panel_radius);
    float in_panel = 1.0 - smoothstep(-panel_feather, panel_feather, d_panel);
    col = mix(col, src.rgb, in_panel);

    // ③ 正中图标（接口预留）
    vec2 icon_uv = (local_px - icon_center) / max(icon_size_px, 1e-6) + vec2(0.5);
    float inside = step(0.0, icon_uv.x) * step(icon_uv.x, 1.0)
                 * step(0.0, icon_uv.y) * step(icon_uv.y, 1.0);
    vec4 icon = texture(icon_tex, clamp(icon_uv, vec2(0.0), vec2(1.0)));
    float ia = icon.a * inside * icon_alpha * step(1.0, icon_size_px);
    col = mix(col, icon.rgb, ia);

    // ④ 这张材质会被本体**复用**（卡组详情页的背景、跑局历史的迷你卡背都拿它的材质），
    //    那些地方贴图不是这张卡框切片 ⇒ UV 根本不在切片里。这时改成"整块用线上侧色单色重染"，
    //    让迷你卡背之类跟着角色色走（原样输出的话，它们会停在"原图红"）。
    vec2 rel = local_px / size;
    float in_frame = step(0.0, rel.x) * step(rel.x, 1.0) * step(0.0, rel.y) * step(rel.y, 1.0);
    //    迷你卡背比卡框亮，直接乘明暗会顶到 255 把色相冲成黄白 ⇒ 按峰值归一，保住色相。
    vec3 ec = color_top.rgb * mix(k, gain, tint_flat);
    ec = ec / max(1.0, max(ec.r, max(ec.g, ec.b)));
    vec3 elsewhere = mix(src.rgb, ec, color_top.a);
    col = mix(elsewhere, col, in_frame);

    float keep = step(0.001, src.a);
    COLOR = vec4(col, max(src.a, ia * in_frame)) * modulate_color * keep;
}
