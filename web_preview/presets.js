/**
 * VideoKit 爆款动态字幕预设库 (移植自 videokit-Y/src/reels-presets-init.js)
 * 适配 1080×1920 竖屏大屏分辨率
 */
const VIDEOKIT_PRESETS = {
    "默认白字": {
        color_text: "#FFFFFF",
        color_high: "#FFD700",
        fontsize: 78,
        bold: true,
        use_stroke: false,
        color_outline: "#000000",
        border_width: 3,
        use_box: false,
        pos_y: 0.82,
        wrap_width_percent: 88,
        anim_in_type: "fade",
        anim_in_duration: 0.25
    },

    "黄字黑边 (经典)": {
        color_text: "#FFD700",
        color_high: "#FF4444",
        fontsize: 86,
        bold: true,
        letter_spacing: 2,
        use_stroke: true,
        color_outline: "#000000",
        border_width: 6,
        use_box: false,
        shadow_blur: 6,
        color_shadow: "#000000",
        pos_y: 0.82,
        wrap_width_percent: 88,
        anim_in_type: "pop",
        anim_in_duration: 0.25
    },

    "Hormozi 风格字幕": {
        font_family: "Anton, Impact, Arial Black",
        color_text: "#FFFFFF",
        color_high: "#FFD700",
        fontsize: 94,
        bold: true,
        letter_spacing: 2,
        use_stroke: true,
        color_outline: "#000000",
        border_width: 8,
        use_box: false,
        shadow_blur: 8,
        color_shadow: "#000000",
        pos_y: 0.78,
        wrap_width_percent: 85,
        anim_in_type: "word_pop_random_pulse",
        anim_in_duration: 0.20
    },

    "金色霓虹环境光": {
        color_text: "#FFFFFF",
        color_high: "#FFFFFF",
        fontsize: 82,
        bold: true,
        use_stroke: false,
        shadow_blur: 15,
        color_shadow: "#FFD700",
        ambient_glow_enabled: true,
        ambient_glow_color: "#FFD700",
        ambient_glow_radius: 400,
        pos_y: 0.82,
        wrap_width_percent: 85,
        anim_in_type: "holy_glow",
        anim_in_duration: 0.35
    },

    "卡拉OK高亮": {
        color_text: "#FFFFFF",
        color_high: "#00E5FF",
        fontsize: 80,
        bold: true,
        use_stroke: true,
        color_outline: "#003366",
        border_width: 5,
        dynamic_box: true,
        color_high_bg: "#00E5FF",
        pos_y: 0.82,
        wrap_width_percent: 88,
        anim_in_type: "karaoke",
        anim_in_duration: 0.3
    },

    "焦糖橙底框高亮": {
        color_text: "#FFFFFF",
        color_high: "#FFE600",
        fontsize: 80,
        bold: true,
        use_box: true,
        color_bg: "#D96800",
        opacity_bg: 245,
        box_radius: 14,
        box_padding_x: 24,
        box_padding_y: 12,
        border_width: 0,
        pos_y: 0.82,
        wrap_width_percent: 88,
        anim_in_type: "pop",
        anim_in_duration: 0.25
    },

    "深海蓝底框高亮": {
        color_text: "#FFFFFF",
        color_high: "#00FFCC",
        fontsize: 80,
        bold: true,
        use_box: true,
        color_bg: "#0B3C73",
        opacity_bg: 245,
        box_radius: 14,
        box_padding_x: 24,
        box_padding_y: 12,
        border_width: 0,
        pos_y: 0.82,
        wrap_width_percent: 88,
        anim_in_type: "pop",
        anim_in_duration: 0.25
    },

    "荧光绿底框_潮黑字": {
        color_text: "#111111",
        color_high: "#000000",
        fontsize: 82,
        bold: true,
        use_box: true,
        color_bg: "#00FF66",
        opacity_bg: 255,
        box_radius: 12,
        box_padding_x: 24,
        box_padding_y: 12,
        border_width: 0,
        pos_y: 0.82,
        wrap_width_percent: 88,
        anim_in_type: "pop",
        anim_in_duration: 0.2
    },

    "暗夜紫底框高亮": {
        color_text: "#FFFFFF",
        color_high: "#FFD700",
        fontsize: 80,
        bold: true,
        use_box: true,
        color_bg: "#4A148C",
        opacity_bg: 240,
        box_radius: 14,
        box_padding_x: 24,
        box_padding_y: 12,
        border_width: 0,
        pos_y: 0.82,
        wrap_width_percent: 88,
        anim_in_type: "pop",
        anim_in_duration: 0.25
    },

    "黑底白字 (新闻/纪实)": {
        color_text: "#FFFFFF",
        color_high: "#FFD700",
        fontsize: 76,
        bold: true,
        use_box: true,
        color_bg: "#000000",
        opacity_bg: 205,
        box_radius: 8,
        box_padding_x: 22,
        box_padding_y: 12,
        border_width: 0,
        pos_y: 0.84,
        wrap_width_percent: 92,
        anim_in_type: "slide_up",
        anim_in_duration: 0.25
    },

    "霓虹蓝粉渐变框": {
        color_text: "#FFFFFF",
        color_high: "#FFFFFF",
        fontsize: 78,
        bold: true,
        use_box: true,
        bg_gradient_enabled: true,
        bg_gradient_colors: "#1E70FF,#9C42C7,#FF2E93",
        opacity_bg: 255,
        box_radius: 14,
        box_padding_x: 26,
        box_padding_y: 14,
        border_width: 0,
        pos_y: 0.82,
        wrap_width_percent: 86,
        anim_in_type: "pop",
        anim_in_duration: 0.25
    },

    "逐词弹出(随机回弹)": {
        color_text: "#FFFFFF",
        color_high: "#FFD700",
        fontsize: 86,
        bold: true,
        letter_spacing: 1,
        use_stroke: true,
        color_outline: "#000000",
        border_width: 5,
        use_box: false,
        shadow_blur: 6,
        color_shadow: "#000000",
        pos_y: 0.80,
        wrap_width_percent: 88,
        anim_in_type: "word_pop_random_pulse",
        anim_in_duration: 0.24
    },

    "打字机模式": {
        color_text: "#F1F5F9",
        color_high: "#FFD700",
        fontsize: 76,
        bold: true,
        use_stroke: true,
        color_outline: "#0F172A",
        border_width: 3,
        use_box: false,
        pos_y: 0.82,
        wrap_width_percent: 88,
        anim_in_type: "typewriter",
        anim_in_duration: 0.35
    },

    "粉色气泡": {
        color_text: "#FFFFFF",
        color_high: "#FF69B4",
        fontsize: 82,
        bold: true,
        use_stroke: true,
        color_outline: "#FF1493",
        border_width: 5,
        shadow_blur: 8,
        color_shadow: "#FF69B4",
        pos_y: 0.82,
        wrap_width_percent: 85,
        anim_in_type: "char_bounce",
        anim_in_duration: 0.35
    },

    "圣光降临": {
        color_text: "#FFFFF0",
        color_high: "#FFD700",
        fontsize: 82,
        bold: true,
        use_stroke: true,
        color_outline: "#8B7500",
        border_width: 3,
        shadow_blur: 12,
        color_shadow: "#FFD700",
        pos_y: 0.82,
        wrap_width_percent: 85,
        anim_in_type: "holy_glow",
        anim_in_duration: 0.45
    }
};

// 预设名称与 ID 别名映射（无缝兼容 SubtitlePresetCatalog 与 videokit-Y）
const PRESET_ALIASES = {
    "box_caramel_orange": "焦糖橙底框高亮",
    "焦糖橙_黄高亮": "焦糖橙底框高亮",
    "box_ocean_blue": "深海蓝底框高亮",
    "深海蓝_黄高亮": "深海蓝底框高亮",
    "box_neon_green_black": "荧光绿底框_潮黑字",
    "荧光绿底框_黑字": "荧光绿底框_潮黑字",
    "box_card_news": "黑底白字 (新闻/纪实)",
    "黑底白字 (新闻)": "黑底白字 (新闻/纪实)",
    "box_neon_pink_blue": "霓虹蓝粉渐变框",
    "box_dark_purple": "暗夜紫底框高亮",
    "暗夜紫_黄高亮": "暗夜紫底框高亮",
    "viral_yellow": "黄字黑边 (经典)",
    "黄字黑边 (经典爆款)": "黄字黑边 (经典)",
    "white_stroke": "默认白字",
    "默认白字 (立体黑边)": "默认白字",
    "mint_green": "荧光薄荷绿",
    "luxury_gold": "金色霓虹环境光",
    "重金大字 (强对比排版)": "金色霓虹环境光",
    "cyber_cyan": "电光青高亮",
    "极简纯文+电光青高亮": "电光青高亮",
    "hormozi_style": "Hormozi 风格字幕",
    "karaoke_blue": "卡拉OK高亮",
    "黑边_电蓝高亮": "卡拉OK高亮",
    "karaoke_pink": "粉色气泡",
    "黑边_粉红高亮": "粉色气泡",
    "karaoke_mint": "荧光薄荷绿",
    "黑边_薄荷高亮": "荧光薄荷绿",
    "pop_bounce": "逐词弹出(随机回弹)",
    "逐词弹出 (动感回弹)": "逐词弹出(随机回弹)",
    "typewriter": "打字机模式"
};

window.findPreset = function(idOrName) {
    if (!idOrName) return VIDEOKIT_PRESETS["黄字黑边 (经典)"];
    if (VIDEOKIT_PRESETS[idOrName]) return VIDEOKIT_PRESETS[idOrName];
    const alias = PRESET_ALIASES[idOrName];
    if (alias && VIDEOKIT_PRESETS[alias]) return VIDEOKIT_PRESETS[alias];

    // 模糊匹配
    for (const key in VIDEOKIT_PRESETS) {
        if (idOrName.includes(key) || key.includes(idOrName)) return VIDEOKIT_PRESETS[key];
    }
    return VIDEOKIT_PRESETS["黄字黑边 (经典)"];
};

window.VIDEOKIT_PRESETS = VIDEOKIT_PRESETS;
window.PRESET_ALIASES = PRESET_ALIASES;
