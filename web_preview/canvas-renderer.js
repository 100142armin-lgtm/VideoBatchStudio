/**
 * VideoKit 60FPS WYSIWYG Canvas Renderer & Interactive Stage
 * 兼容 9:16 / 16:9 画布，支持鼠标直接拖拽微调与 C# 双向联动
 */

class PreviewStage {
    constructor() {
        this.container = document.getElementById('stage-container');
        this.shell = document.getElementById('phone-shell');
        this.videoCanvas = document.getElementById('video-canvas');
        this.vctx = this.videoCanvas.getContext('2d');
        this.overlayCanvas = document.getElementById('overlay-canvas');
        this.octx = this.overlayCanvas.getContext('2d');

        // 逻辑分辨率基准：1080 x 1920
        this.logicalW = 1080;
        this.logicalH = 1920;
        this.scale = 1.0;

        // 动效与时间轴
        this.currentTimeMs = 1200;
        this.isPlaying = false;
        this.lastFrameTime = performance.now();
        this.fpsCounter = 0;
        this.fpsLastTime = performance.now();

        // 默认状态参数（由 C# 传入覆盖）
        this.state = {
            subtitleText: "Senhora que está segurando um telefone, talvez eu não saiba o seu nome, mas você é abençoada.",
            subtitleStyleId: "焦糖橙底框高亮",
            subtitleFontSize: 86,
            subtitleVerticalPercent: 82,
            subtitleOffsetX: 0,
            subtitleFontFamily: "Arial",

            watermarkEnabled: true,
            watermarkType: 0, // 0: 文字, 1: 图片
            watermarkText: "Voice: ElevenLabs",
            watermarkFontSize: 28,
            watermarkTextColorHex: "#FFFFFF",
            watermarkTextOpacity: 85,
            watermarkImagePath: "",
            watermarkImageScalePercent: 15,
            watermarkImageOpacity: 85,
            watermarkPosition: 0,
            watermarkOffsetX: 35,
            watermarkOffsetY: 40,

            bgThumbnailDataUrl: ""
        };

        // 交互拖拽状态
        this.dragTarget = null; // 'subtitle' | 'watermark' | null
        this.dragStartPos = { x: 0, y: 0 };
        this.initialOffsets = { x: 0, y: 0 };

        this.bgImage = null;
        this.wmImage = null;

        this.initDOM();
        this.bindEvents();
    }

    initDOM() {
        this.resize();
        window.addEventListener('resize', () => this.resize());
    }

    resize() {
        const cw = Math.max(100, this.container.clientWidth - 20);
        const ch = Math.max(100, this.container.clientHeight - 20);
        const targetAspect = this.logicalW / this.logicalH; // 9:16 = 0.5625

        let displayW, displayH;
        if (cw / ch > targetAspect) {
            displayH = ch;
            displayW = Math.round(displayH * targetAspect);
        } else {
            displayW = cw;
            displayH = Math.round(displayW / targetAspect);
        }

        // 严格安全限制：保证手机壳绝不出画、绝不裁剪底部
        if (displayH > ch) {
            displayH = ch;
            displayW = Math.round(displayH * targetAspect);
        }
        if (displayW > cw) {
            displayW = cw;
            displayH = Math.round(displayW / targetAspect);
        }

        this.shell.style.width = `${displayW}px`;
        this.shell.style.height = `${displayH}px`;

        const dpr = window.devicePixelRatio || 1;
        this.videoCanvas.width = displayW * dpr;
        this.videoCanvas.height = displayH * dpr;
        this.overlayCanvas.width = displayW * dpr;
        this.overlayCanvas.height = displayH * dpr;

        this.vctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        this.octx.setTransform(dpr, 0, 0, dpr, 0, 0);

        this.renderWidth = displayW;
        this.renderHeight = displayH;
        this.scale = displayW / this.logicalW;

        this.requestRender();
    }

    applyHostSettings(settings) {
        if (!settings) return;
        Object.assign(this.state, settings);

        if (Number(this.state.subtitleDisplayMode) === 3 && !this._autoStartedHighlight) {
            this._autoStartedHighlight = true;
            this.currentTimeMs = 0;
            this.isPlaying = true;
            const btn = document.getElementById('btn-play');
            if (btn) {
                btn.textContent = '⏸️ 暂停';
                btn.classList.add('active');
            }
        }

        // 背景图与水印图载入
        if (settings.bgThumbnailDataUrl && settings.bgThumbnailDataUrl !== this.cachedBgUrl) {
            this.cachedBgUrl = settings.bgThumbnailDataUrl;
            this.bgImage = new Image();
            this.bgImage.onload = () => this.requestRender();
            this.bgImage.src = settings.bgThumbnailDataUrl;
        }

        const wmSrc = settings.watermarkImageDataUrl || settings.watermarkImagePath;
        if (wmSrc && wmSrc !== this.cachedWmPath) {
            this.cachedWmPath = wmSrc;
            this.wmImage = new Image();
            this.wmImage.onload = () => this.requestRender();
            this.wmImage.src = wmSrc;
        }

        this.requestRender();
    }

    start() {
        const loop = (now) => {
            const dt = now - this.lastFrameTime;
            this.lastFrameTime = now;

            // 统计 FPS
            this.fpsCounter++;
            if (now - this.fpsLastTime >= 1000) {
                const fpsBadge = document.getElementById('fps-badge');
                if (fpsBadge) fpsBadge.textContent = `${this.fpsCounter} FPS`;
                this.fpsCounter = 0;
                this.fpsLastTime = now;
            }

            if (this.isPlaying) {
                this.currentTimeMs += dt;
                if (this.currentTimeMs > 3000) this.currentTimeMs = 0;
                const slider = document.getElementById('time-slider');
                const label = document.getElementById('time-label');
                if (slider) slider.value = Math.round(this.currentTimeMs);
                if (label) label.textContent = `${(this.currentTimeMs / 1000).toFixed(2)}s`;
                this.render();
            }

            requestAnimationFrame(loop);
        };
        requestAnimationFrame(loop);
        this.requestRender();
    }

    requestRender() {
        requestAnimationFrame(() => this.render());
    }

    render() {
        const w = this.renderWidth;
        const h = this.renderHeight;
        if (!w || !h) return;

        const vctx = this.vctx;
        const octx = this.octx;
        const scale = this.scale;

        vctx.clearRect(0, 0, w, h);
        octx.clearRect(0, 0, w, h);

        // 1. 绘制背景 (Cover 居中等比裁剪模式，杜绝拉伸畸变)
        if (this.bgImage && this.bgImage.complete && this.bgImage.naturalWidth > 0) {
            const img = this.bgImage;
            const targetAspect = w / h;
            const imgAspect = img.naturalWidth / img.naturalHeight;
            let sx, sy, sWidth, sHeight;
            if (imgAspect > targetAspect) {
                sHeight = img.naturalHeight;
                sWidth = Math.round(sHeight * targetAspect);
                sx = Math.round((img.naturalWidth - sWidth) / 2);
                sy = 0;
            } else {
                sWidth = img.naturalWidth;
                sHeight = Math.round(sWidth / targetAspect);
                sx = 0;
                sy = Math.round((img.naturalHeight - sHeight) / 2);
            }
            vctx.drawImage(img, sx, sy, sWidth, sHeight, 0, 0, w, h);
        } else {
            const grad = vctx.createLinearGradient(0, 0, w, h);
            grad.addColorStop(0, '#0F172A');
            grad.addColorStop(0.5, '#1E293B');
            grad.addColorStop(1, '#090D16');
            vctx.fillStyle = grad;
            vctx.fillRect(0, 0, w, h);

            // 极简参考网格
            vctx.strokeStyle = 'rgba(255, 255, 255, 0.04)';
            vctx.lineWidth = 1;
            for (let x = 0; x < w; x += 30) { vctx.beginPath(); vctx.moveTo(x, 0); vctx.lineTo(x, h); vctx.stroke(); }
            for (let y = 0; y < h; y += 30) { vctx.beginPath(); vctx.moveTo(0, y); vctx.lineTo(w, y); vctx.stroke(); }
        }

        // 2. 绘制字幕层 (调用 VideoKit 预设与动画引擎)
        this.renderSubtitles(vctx, w, h, scale);

        // 3. 绘制水印层 (支持多行自动折行与拖拽虚线框)
        this.renderWatermark(vctx, octx, w, h, scale);

        // 4. 绘制真机全景细节 (灵动岛与底部小白条，杜绝上下截断误觉)
        if (h > w) {
            octx.save();
            // 灵动岛 (Dynamic Island)
            const islandW = Math.max(50, w * 0.28);
            const islandH = Math.max(12, h * 0.022);
            const islandX = (w - islandW) / 2;
            const islandY = 6;
            octx.fillStyle = 'rgba(15, 23, 42, 0.95)';
            if (octx.roundRect) {
                octx.beginPath();
                octx.roundRect(islandX, islandY, islandW, islandH, islandH / 2);
                octx.fill();
            } else {
                octx.fillRect(islandX, islandY, islandW, islandH);
            }
            // 镜头光点
            octx.fillStyle = 'rgba(59, 130, 246, 0.45)';
            octx.beginPath();
            octx.arc(islandX + islandW - 14, islandY + islandH / 2, 3, 0, Math.PI * 2);
            octx.fill();

            // 底部手势小白条 (Home Indicator)
            const barW = Math.max(60, w * 0.36);
            const barH = 3;
            const barX = (w - barW) / 2;
            const barY = h - 10;
            octx.fillStyle = 'rgba(255, 255, 255, 0.65)';
            if (octx.roundRect) {
                octx.beginPath();
                octx.roundRect(barX, barY, barW, barH, 1.5);
                octx.fill();
            } else {
                octx.fillRect(barX, barY, barW, barH);
            }
            octx.restore();
        }
    }

    renderSubtitles(ctx, w, h, scale) {
        const displayMode = Number(this.state.subtitleDisplayMode || 0);
        const fullText = this.state.subtitleText || "Senhora que está segurando um telefone, você é abençoada.";
        const text = (displayMode === 1 || displayMode === 3) ? fullText.trim().split(/\s+/).slice(0, 4).join(' ') : fullText;
        const styleId = this.state.subtitleStyleId || "焦糖橙底框高亮";
        const basePreset = (window.findPreset && window.findPreset(styleId)) || (window.VIDEOKIT_PRESETS && window.VIDEOKIT_PRESETS[styleId]) || window.VIDEOKIT_PRESETS["默认白字"] || {};
        const preset = {
            ...basePreset,
            color_text: this.state.subtitleTextColor || basePreset.color_text || '#FFFFFF',
            color_outline: this.state.subtitleStrokeColor || basePreset.color_outline,
            border_width: Number(this.state.subtitleStrokeWidth ?? basePreset.border_width ?? 0),
            use_stroke: Number(this.state.subtitleStrokeWidth ?? basePreset.border_width ?? 0) > 0,
            use_box: Number(this.state.subtitleBorderStyle ?? 1) === 3,
            color_bg: this.state.subtitlePanelColor || basePreset.color_bg,
            opacity_bg: Math.round(Number(this.state.subtitlePanelOpacity ?? 62) * 2.55)
        };

        const fontSize = Math.max(10, (this.state.subtitleFontSize || preset.fontsize || 86) * scale);
        const fontFamily = this.state.subtitleFontFamily || preset.font_family || 'Arial, -apple-system, sans-serif';
        const fontWeight = preset.bold !== false ? 'bold' : 'normal';

        ctx.font = `${fontWeight} ${fontSize}px ${fontFamily}`;
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';

        // 计算 Y 轴与 X 轴位置
        const posYPercent = (this.state.subtitleVerticalPercent || 82) / 100.0;
        const subCenterY = h * posYPercent;
        const subShiftX = (this.state.subtitleOffsetX || 0) * scale;
        const subCenterX = w / 2 + subShiftX;

        // 智能折行（每行最大 88% 宽度）
        const maxSubW = w * 0.88;
        const words = text.split(' ');
        const lines = [];
        let currentLine = words[0] || '';

        for (let i = 1; i < words.length; i++) {
            const word = words[i];
            const testLine = currentLine + ' ' + word;
            if (ctx.measureText(testLine).width > maxSubW) {
                lines.push(currentLine);
                currentLine = word;
            } else {
                currentLine = testLine;
            }
        }
        lines.push(currentLine);

        const lineHeight = fontSize * 1.25;
        const totalTextH = lines.length * lineHeight;
        const startY = subCenterY - (totalTextH / 2) + (lineHeight / 2);

        // 记录字幕触控与拖拽包围盒
        let maxLineW = 0;
        lines.forEach(l => {
            const lw = ctx.measureText(l).width;
            if (lw > maxLineW) maxLineW = lw;
        });
        const boxPadX = preset.use_box ? (preset.box_padding_x || 20) * scale : 16 * scale;
        const boxPadY = preset.use_box ? (preset.box_padding_y || 10) * scale : 10 * scale;
        this.subtitleBounds = {
            x: subCenterX - (maxLineW / 2) - boxPadX,
            y: subCenterY - (totalTextH / 2) - boxPadY,
            w: maxLineW + boxPadX * 2,
            h: totalTextH + boxPadY * 2
        };

        // 动效模拟系数
        let animScale = 1.0;
        if (preset.anim_in_type === 'word_pop_random_pulse' || preset.anim_in_type === 'pop') {
            const cycle = (this.currentTimeMs % 600) / 600;
            if (cycle < 0.3) animScale = 1.0 + Math.sin((cycle / 0.3) * Math.PI) * 0.12;
        }

        ctx.save();
        ctx.translate(subCenterX, subCenterY);
        ctx.scale(animScale, animScale);
        ctx.translate(-subCenterX, -subCenterY);
        ctx.shadowColor = this.state.subtitleShadowColor || 'transparent';
        ctx.shadowOffsetX = Number(this.state.subtitleShadowDepth || 0) * scale;
        ctx.shadowOffsetY = Number(this.state.subtitleShadowDepth || 0) * scale;
        ctx.shadowBlur = Number(this.state.subtitleGlowBlur || 0) * scale;

        let globalWordIndex = 0;
        const wordCount = text.trim().split(/\s+/).length;
        const activeWordIndex = wordCount > 0 ? Math.floor(this.currentTimeMs / 450) % wordCount : -1;
        lines.forEach((line, idx) => {
            const lineY = startY + (idx * lineHeight);
            const metrics = ctx.measureText(line);
            const lineW = metrics.width;

            if (displayMode === 1 || displayMode === 3) {
                const lineWords = line.trim().split(/\s+/);
                ctx.textAlign = 'left';
                const spaceW = ctx.measureText(' ').width;
                let x = subCenterX - lineW / 2;
                for (const word of lineWords) {
                    const wordW = ctx.measureText(word).width;
                    const isActive = globalWordIndex === activeWordIndex;
                    if (isActive) {
                        const padX = 10 * scale;
                        const padY = 5 * scale;
                        if (displayMode === 3 && this.state.subtitleHighlightBoxEnabled !== false) {
                            ctx.save();
                            ctx.globalAlpha = Math.max(0.1, Math.min(1, Number(this.state.subtitleHighlightBoxOpacity || 100) / 100));
                            ctx.fillStyle = this.state.subtitleHighlightBgColor || '#D01010';
                            this.drawRoundedRect(ctx, x - padX, lineY - fontSize / 2 - padY, wordW + 2 * padX, fontSize + 2 * padY, 5 * scale);
                            ctx.fill();
                            ctx.restore();
                        }
                        ctx.fillStyle = this.state.subtitleHighlightTextColor || '#FFD700';
                    } else {
                        ctx.fillStyle = preset.color_text || '#FFFFFF';
                    }
                    if ((!isActive || displayMode === 1 || this.state.subtitleHighlightBoxEnabled === false) && preset.use_stroke && preset.border_width > 0) {
                        ctx.strokeStyle = preset.color_outline || '#6488A8';
                        ctx.lineWidth = Math.max(1, preset.border_width * scale);
                        ctx.lineJoin = 'round';
                        ctx.strokeText(word, x, lineY);
                    }
                    ctx.save();
                    ctx.globalAlpha = Math.max(0.2, Math.min(1, Number(this.state.subtitleTextOpacity || 100) / 100));
                    ctx.fillText(word, x, lineY);
                    ctx.restore();
                    x += wordW + spaceW;
                    globalWordIndex++;
                }
                ctx.textAlign = 'center';
                return;
            }

            // 1) 绘制底框 (BorderStyle == 3 / use_box)
            if (preset.use_box) {
                const padX = (preset.box_padding_x || 20) * scale;
                const padY = (preset.box_padding_y || 10) * scale;
                const radius = Math.max(4, (preset.box_radius || 12) * scale);

                const boxL = subCenterX - (lineW / 2) - padX;
                const boxT = lineY - (fontSize / 2) - padY;
                const boxW = lineW + padX * 2;
                const boxH = fontSize + padY * 2;

                ctx.save();
                if (preset.bg_gradient_enabled && preset.bg_gradient_colors) {
                    const colors = preset.bg_gradient_colors.split(',');
                    const grad = ctx.createLinearGradient(boxL, boxT, boxL + boxW, boxT);
                    grad.addColorStop(0, colors[0] || '#1E70FF');
                    grad.addColorStop(1, colors[colors.length - 1] || '#FF2E93');
                    ctx.fillStyle = grad;
                } else {
                    ctx.fillStyle = preset.color_bg || '#D96800';
                }
                ctx.globalAlpha = (preset.opacity_bg || 240) / 255.0;
                this.drawRoundedRect(ctx, boxL, boxT, boxW, boxH, radius);
                ctx.fill();
                ctx.restore();
            }

            // 2) 绘制描边
            if (preset.use_stroke && preset.border_width > 0) {
                ctx.strokeStyle = preset.color_outline || '#000000';
                ctx.lineWidth = Math.max(2, (preset.border_width || 5) * scale);
                ctx.lineJoin = 'round';
                ctx.strokeText(line, subCenterX, lineY);
            }

            // 3) 绘制文字主体与高亮
            ctx.fillStyle = preset.color_text || '#FFFFFF';
            ctx.save();
            ctx.globalAlpha = Math.max(0.2, Math.min(1, Number(this.state.subtitleTextOpacity || 100) / 100));
            ctx.fillText(line, subCenterX, lineY);
            ctx.restore();
        });

        ctx.restore();
    }

    renderWatermark(vctx, octx, w, h, scale) {
        const isEnabled = this.state.watermarkEnabled;
        const posType = this.state.watermarkPosition || 0;
        const offX = (this.state.watermarkOffsetX || 0) * scale;
        const offY = (this.state.watermarkOffsetY || 0) * scale;

        // 绘制字幕边框高亮（当鼠标悬停或拖拽时）
        if (this.subtitleBounds) {
            const sb = this.subtitleBounds;
            octx.save();
            octx.strokeStyle = 'rgba(245, 158, 11, 0.85)';
            octx.lineWidth = 1.5;
            octx.setLineDash([4, 4]);
            octx.strokeRect(sb.x - 2, sb.y - 2, sb.w + 4, sb.h + 4);
            octx.fillStyle = '#F59E0B';
            octx.font = 'bold 11px monospace';
            octx.fillText(`[字幕 Y:${this.state.subtitleVerticalPercent}% X:${this.state.subtitleOffsetX}px]`, sb.x - 2, Math.max(12, sb.y - 6));
            octx.restore();
        }

        if (this.state.watermarkType === 0) { // 文字水印
            const rawText = this.state.watermarkText || "Voice: ElevenLabs";
            const fontSize = Math.max(10, (this.state.watermarkFontSize || 28) * scale);
            const opacity = Math.max(0.1, (this.state.watermarkTextOpacity || 80) / 100.0);

            vctx.save();
            vctx.font = `bold ${fontSize}px "Microsoft YaHei", Arial, sans-serif`;
            vctx.textBaseline = 'top';

            // 支持手动回车换行与超长行自动折行，保证长文本不溢出画面边缘
            const rawLines = rawText.split(/\r?\n/);
            const maxAllowedW = w * 0.85;
            let lines = [];
            rawLines.forEach(rl => {
                if (vctx.measureText(rl).width <= maxAllowedW) {
                    lines.push(rl);
                } else {
                    let cur = "";
                    for (let ch of rl) {
                        if (vctx.measureText(cur + ch).width > maxAllowedW) {
                            lines.push(cur);
                            cur = ch;
                        } else {
                            cur += ch;
                        }
                    }
                    if (cur) lines.push(cur);
                }
            });
            if (lines.length === 0) lines = [""];

            let maxLineW = 0;
            lines.forEach(l => {
                const lw = vctx.measureText(l).width;
                if (lw > maxLineW) maxLineW = lw;
            });
            const lineH = fontSize * 1.3;
            const totalH = lines.length * lineH;

            const pt = this.calcPosition(posType, w, h, maxLineW, totalH, offX, offY);

            // 绘制文字实体到 videoCanvas
            lines.forEach((l, idx) => {
                const y = pt.y + idx * lineH;
                vctx.fillStyle = `rgba(0, 0, 0, ${opacity * 0.7})`;
                vctx.fillText(l, pt.x + 1, y + 1);
                vctx.fillStyle = this.state.watermarkTextColorHex || '#FFFFFF';
                vctx.globalAlpha = opacity;
                vctx.fillText(l, pt.x, y);
            });
            vctx.restore();

            // 绘制交互选框到 overlayCanvas
            this.drawSelectionBox(octx, pt.x, pt.y, maxLineW, totalH, isEnabled, `[水印 X:${Math.round(offX/scale)} Y:${Math.round(offY/scale)}]`);
            this.watermarkBounds = { x: pt.x, y: pt.y, w: maxLineW, h: totalH };

        } else if (this.state.watermarkType === 1 && this.wmImage && this.wmImage.complete) { // 图片水印
            const img = this.wmImage;
            const imgScale = (this.state.watermarkImageScalePercent || 15) / 100.0;
            const targetW = w * imgScale;
            const targetH = img.naturalHeight * (targetW / img.naturalWidth);
            const opacity = (this.state.watermarkImageOpacity || 85) / 100.0;

            const pt = this.calcPosition(posType, w, h, targetW, targetH, offX, offY);

            vctx.save();
            vctx.globalAlpha = opacity;
            vctx.drawImage(img, pt.x, pt.y, targetW, targetH);
            vctx.restore();

            this.drawSelectionBox(octx, pt.x, pt.y, targetW, targetH, isEnabled, `[图片水印 X:${Math.round(offX/scale)} Y:${Math.round(offY/scale)}]`);
            this.watermarkBounds = { x: pt.x, y: pt.y, w: targetW, h: targetH };
        }
    }

    drawSelectionBox(ctx, x, y, w, h, isEnabled, label) {
        ctx.save();
        ctx.strokeStyle = isEnabled ? '#3B82F6' : '#94A3B8';
        ctx.lineWidth = 1.5;
        ctx.setLineDash([4, 4]);
        ctx.strokeRect(x - 3, y - 3, w + 6, h + 6);

        // 标签指示
        ctx.fillStyle = isEnabled ? '#3B82F6' : '#94A3B8';
        ctx.font = 'bold 11px monospace';
        ctx.fillText(label, x - 2, Math.max(12, y - 8));
        ctx.restore();
    }

    calcPosition(pos, w, h, itemW, itemH, offX, offY) {
        let x = offX;
        let y = offY;
        switch (pos) {
            case 0: x = w - itemW - offX; y = h - itemH - offY; break; // 右下
            case 1: x = w - itemW - offX; y = offY; break;             // 右上
            case 2: x = offX; y = h - itemH - offY; break;             // 左下
            case 3: x = offX; y = offY; break;                         // 左上
            case 4: x = (w - itemW) / 2 + offX; y = h - itemH - offY; break; // 底中
            case 5: x = (w - itemW) / 2 + offX; y = offY; break;             // 顶中
            default: x = offX; y = offY; break;
        }
        return { x: Math.max(0, x), y: Math.max(0, y) };
    }

    drawRoundedRect(ctx, x, y, width, height, radius) {
        ctx.beginPath();
        ctx.moveTo(x + radius, y);
        ctx.lineTo(x + width - radius, y);
        ctx.quadraticCurveTo(x + width, y, x + width, y + radius);
        ctx.lineTo(x + width, y + height - radius);
        ctx.quadraticCurveTo(x + width, y + height, x + width - radius, y + height);
        ctx.lineTo(x + radius, y + height);
        ctx.quadraticCurveTo(x, y + height, x, y + height - radius);
        ctx.lineTo(x, y + radius);
        ctx.quadraticCurveTo(x, y, x + radius, y);
        ctx.closePath();
    }

    bindEvents() {
        const btnPlay = document.getElementById('btn-play');
        const btnReset = document.getElementById('btn-reset');
        const slider = document.getElementById('time-slider');
        const label = document.getElementById('time-label');

        if (btnPlay) {
            btnPlay.addEventListener('click', () => {
                this.isPlaying = !this.isPlaying;
                btnPlay.textContent = this.isPlaying ? '⏸️ 暂停' : '▶️ 动效';
                btnPlay.classList.toggle('active', this.isPlaying);
            });
        }
        if (btnReset) {
            btnReset.addEventListener('click', () => {
                this.currentTimeMs = 0;
                if (slider) slider.value = 0;
                if (label) label.textContent = '0.00s';
                this.requestRender();
            });
        }
        if (slider) {
            slider.addEventListener('input', (e) => {
                this.currentTimeMs = parseFloat(e.target.value);
                if (label) label.textContent = `${(this.currentTimeMs / 1000).toFixed(2)}s`;
                this.requestRender();
            });
        }

        // 鼠标直接在手机屏幕上拖拽水印坐标或字幕坐标
        this.overlayCanvas.addEventListener('mousedown', (e) => {
            const rect = this.overlayCanvas.getBoundingClientRect();
            const mx = e.clientX - rect.left;
            const my = e.clientY - rect.top;

            if (this.watermarkBounds) {
                const b = this.watermarkBounds;
                if (mx >= b.x - 10 && mx <= b.x + b.w + 10 && my >= b.y - 10 && my <= b.y + b.h + 10) {
                    this.dragTarget = 'watermark';
                    this.dragStartPos = { x: mx, y: my };
                    this.initialOffsets = { x: this.state.watermarkOffsetX, y: this.state.watermarkOffsetY };
                    this.shell.classList.add('highlight-active');
                    return;
                }
            }

            if (this.subtitleBounds) {
                const b = this.subtitleBounds;
                if (mx >= b.x - 10 && mx <= b.x + b.w + 10 && my >= b.y - 10 && my <= b.y + b.h + 10) {
                    this.dragTarget = 'subtitle';
                    this.dragStartPos = { x: mx, y: my };
                    this.initialOffsets = { x: this.state.subtitleOffsetX, percent: this.state.subtitleVerticalPercent };
                    this.shell.classList.add('highlight-active');
                    return;
                }
            }
        });

        window.addEventListener('mousemove', (e) => {
            if (!this.dragTarget) {
                // 鼠标悬停变手型
                const rect = this.overlayCanvas.getBoundingClientRect();
                const mx = e.clientX - rect.left;
                const my = e.clientY - rect.top;
                let isHover = false;
                if (this.watermarkBounds) {
                    const b = this.watermarkBounds;
                    if (mx >= b.x - 6 && mx <= b.x + b.w + 6 && my >= b.y - 6 && my <= b.y + b.h + 6) isHover = true;
                }
                if (!isHover && this.subtitleBounds) {
                    const b = this.subtitleBounds;
                    if (mx >= b.x - 6 && mx <= b.x + b.w + 6 && my >= b.y - 6 && my <= b.y + b.h + 6) isHover = true;
                }
                this.overlayCanvas.style.cursor = isHover ? 'grab' : 'default';
                return;
            }

            this.overlayCanvas.style.cursor = 'grabbing';
            const rect = this.overlayCanvas.getBoundingClientRect();
            const mx = e.clientX - rect.left;
            const my = e.clientY - rect.top;

            const dx = (mx - this.dragStartPos.x) / this.scale;
            const dy = (my - this.dragStartPos.y) / this.scale;

            if (this.dragTarget === 'watermark') {
                let signX = -1;
                let signY = -1;
                const pos = this.state.watermarkPosition || 0;
                if (pos === 2 || pos === 3 || pos === 4 || pos === 5 || pos === 6) signX = 1;
                if (pos === 1 || pos === 3 || pos === 5 || pos === 6) signY = 1;

                const newX = Math.max(0, Math.min(1000, Math.round(this.initialOffsets.x + dx * signX)));
                const newY = Math.max(0, Math.min(1800, Math.round(this.initialOffsets.y + dy * signY)));
                this.state.watermarkOffsetX = newX;
                this.state.watermarkOffsetY = newY;
                this.requestRender();

                // 实时双向回传给 C# WinForms
                if (window.notifyHost) {
                    window.notifyHost('watermark_drag', { x: newX, y: newY });
                }
            } else if (this.dragTarget === 'subtitle') {
                const newOffsetX = Math.max(-350, Math.min(350, Math.round(this.initialOffsets.x + dx)));
                const dPercent = ((my - this.dragStartPos.y) / this.renderHeight) * 100;
                const newPercent = Math.max(15, Math.min(88, Math.round(this.initialOffsets.percent + dPercent)));

                this.state.subtitleOffsetX = newOffsetX;
                this.state.subtitleVerticalPercent = newPercent;
                this.requestRender();

                if (window.notifyHost) {
                    window.notifyHost('subtitle_drag', { x: newOffsetX, percent: newPercent });
                }
            }
        });

        window.addEventListener('mouseup', () => {
            if (this.dragTarget) {
                this.dragTarget = null;
                this.overlayCanvas.style.cursor = 'default';
                this.shell.classList.remove('highlight-active');
            }
        });
    }
}
