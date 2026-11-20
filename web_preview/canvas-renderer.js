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
            subtitleBold: true,
            subtitleItalic: false,
            subtitleCursorStyle: 0,
            subtitleVisibleLines: 3,

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
        const fontWeight = this.state.subtitleBold !== false ? 'bold' : 'normal';
        const fontStyle = this.state.subtitleItalic ? 'italic' : 'normal';
        const visibleLines = Math.max(1, Math.min(3, Number(this.state.subtitleVisibleLines || 3)));
        const cursorStyle = Number(this.state.subtitleCursorStyle || 0);

        ctx.font = `${fontStyle} ${fontWeight} ${fontSize}px "${fontFamily}", ${fontFamily}, sans-serif`;
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

        const textAlpha = Math.max(0.2, Math.min(1, Number(this.state.subtitleTextOpacity || 100) / 100));

        // ============================================================
        // Mode 6: 白字粉边·红花游标跟读 (1:1 复刻 60K 绿水滴视频)
        // ============================================================
        if (displayMode === 6) {
            const shownLines = lines.slice(0, visibleLines);
            const totalChars = shownLines.reduce((acc, l) => acc + l.length, 0) || 1;
            const cycleMs = 4200;
            const t = (this.currentTimeMs % cycleMs) / cycleMs;
            const revealProgress = Math.min(1, t / 0.78);
            const fadeAlpha = t > 0.90 ? Math.max(0, 1 - (t - 0.90) / 0.10) : 1;
            const revealedCount = Math.max(1, Math.floor(revealProgress * totalChars));
            const mode6LineH = fontSize * 1.28;
            const mode6TotalH = shownLines.length * mode6LineH;
            const mode6StartY = subCenterY - mode6TotalH / 2 + mode6LineH / 2;

            let charsLeft = revealedCount;
            let tipX = subCenterX;
            let tipY = mode6StartY;

            ctx.save();
            ctx.globalAlpha = textAlpha * fadeAlpha;
            for (let li = 0; li < shownLines.length; li++) {
                const fullLine = shownLines[li];
                const ly = mode6StartY + li * mode6LineH;
                const fullW = ctx.measureText(fullLine).width;
                const startX = subCenterX - fullW / 2;
                const take = Math.max(0, Math.min(fullLine.length, charsLeft));
                charsLeft -= take;
                if (take <= 0) continue;

                const part = fullLine.slice(0, take);
                const isActiveLine = charsLeft === 0 && revealProgress < 1;
                // 已读词纯白，当前整个正在读的词亮金黄
                let settledPart = part;
                let activeWordPart = '';
                if (isActiveLine) {
                    const lastSpace = part.lastIndexOf(' ');
                    settledPart = lastSpace >= 0 ? part.slice(0, lastSpace + 1) : '';
                    activeWordPart = lastSpace >= 0 ? part.slice(lastSpace + 1) : part;
                }

                ctx.textAlign = 'left';
                if (preset.use_stroke && preset.border_width > 0) {
                    ctx.strokeStyle = preset.color_outline || '#E01686';
                    ctx.lineWidth = Math.max(2, preset.border_width * scale * 2);
                    ctx.lineJoin = 'round';
                    ctx.strokeText(part, startX, ly);
                }
                if (settledPart) {
                    ctx.fillStyle = preset.color_text || '#FFFFFF';
                    ctx.fillText(settledPart, startX, ly);
                }
                if (activeWordPart) {
                    const settledW = settledPart ? ctx.measureText(settledPart).width : 0;
                    ctx.fillStyle = this.state.subtitleHighlightTextColor || '#FFE029';
                    ctx.fillText(activeWordPart, startX + settledW, ly);
                }
                tipX = startX + ctx.measureText(part).width;
                tipY = ly - fontSize * 0.08;
            }
            if (cursorStyle > 0 && fadeAlpha > 0.1) {
                this.drawFollowerCursor(ctx, tipX, tipY, fontSize, cursorStyle, scale);
            }
            ctx.restore();
            ctx.restore();
            return;
        }

        // ============================================================
        // Mode 7: 暗紫纱条·金笔划词跟读 (1:1 复刻 207K 金色 17 号视频)
        // ============================================================
        if (displayMode === 7) {
            const shownLines = lines.slice(0, visibleLines);
            const barLineH = fontSize * 1.40;
            const mode7TotalH = shownLines.length * barLineH;
            const mode7StartY = subCenterY - mode7TotalH / 2 + barLineH / 2;
            const panelEnabled = this.state.subtitleScrollPanelEnabled !== false;
            const panelAlpha = Math.max(0.1, Math.min(0.95, Number(this.state.subtitlePanelOpacity ?? 74) / 100));
            const panelColor = this.state.subtitlePanelColor || '#2E1E42';

            // 计算黑金笔当前扫过哪一行、哪一位置
            const totalChars = shownLines.reduce((acc, l) => acc + Math.max(1, l.length), 0) || 1;
            const cycleMs = 4500;
            const prog = (this.currentTimeMs % cycleMs) / cycleMs;
            let penChars = Math.floor(prog * totalChars);
            let tipX = subCenterX;
            let tipY = mode7StartY;

            for (let li = 0; li < shownLines.length; li++) {
                const fullLine = shownLines[li];
                const ly = mode7StartY + li * barLineH;
                const fullW = ctx.measureText(fullLine).width;
                const startX = subCenterX - fullW / 2;

                if (panelEnabled) {
                    const padX = 26 * scale;
                    const padY = 10 * scale;
                    const barW = fullW + padX * 2;
                    const barH = fontSize + padY * 2;
                    ctx.save();
                    ctx.shadowColor = 'transparent';
                    ctx.globalAlpha = panelAlpha;
                    ctx.fillStyle = panelColor;
                    this.drawRoundedRect(ctx, subCenterX - barW / 2, ly - barH / 2, barW, barH, 14 * scale);
                    ctx.fill();
                    ctx.restore();
                }

                ctx.textAlign = 'left';
                if (preset.use_stroke && preset.border_width > 0) {
                    ctx.strokeStyle = preset.color_outline || '#1C0F2E';
                    ctx.lineWidth = Math.max(1.5, preset.border_width * scale * 2);
                    ctx.lineJoin = 'round';
                    ctx.strokeText(fullLine, startX, ly);
                }
                ctx.save();
                ctx.globalAlpha = textAlpha;
                ctx.fillStyle = preset.color_text || '#FFFFFF';
                ctx.fillText(fullLine, startX, ly);
                ctx.restore();

                if (penChars >= 0 && penChars <= fullLine.length) {
                    const sub = fullLine.slice(0, penChars);
                    tipX = startX + ctx.measureText(sub).width;
                    tipY = ly + fontSize * 0.20;
                    penChars = -1;
                } else if (penChars > fullLine.length) {
                    penChars -= fullLine.length;
                }
            }

            if (cursorStyle > 0) {
                this.drawFollowerCursor(ctx, tipX, tipY, fontSize, cursorStyle, scale);
            }
            ctx.restore();
            return;
        }

        // ============================================================
        // Mode 8: 三行黑边·红带累计推进 (1:1 复刻 15 号粉色数字视频)
        // ============================================================
        if (displayMode === 8) {
            const shownLines = lines.slice(0, visibleLines);
            const mode8LineH = fontSize * 1.24;
            const mode8TotalH = shownLines.length * mode8LineH;
            const mode8StartY = subCenterY - mode8TotalH / 2 + mode8LineH / 2;

            const lineWordCounts = shownLines.map(l => Math.max(1, l.trim().split(/\s+/).length));
            const totalWords = lineWordCounts.reduce((a, b) => a + b, 0) || 1;
            const currentGlobalWord = Math.floor(this.currentTimeMs / 420) % totalWords;
            let activeLineIdx = 0;
            let activeWordInLine = 0;
            let acc = 0;
            for (let li = 0; li < lineWordCounts.length; li++) {
                if (currentGlobalWord < acc + lineWordCounts[li]) {
                    activeLineIdx = li;
                    activeWordInLine = currentGlobalWord - acc;
                    break;
                }
                acc += lineWordCounts[li];
            }

            const ribbonEnabled = this.state.subtitleHighlightBoxEnabled !== false;
            const ribbonColor = this.state.subtitleHighlightBgColor || '#E01B24';
            const ribbonAlpha = Math.max(0.15, Math.min(1, Number(this.state.subtitleHighlightBoxOpacity || 96) / 100));

            for (let li = 0; li < shownLines.length; li++) {
                const line = shownLines[li];
                const ly = mode8StartY + li * mode8LineH;
                const lineW = ctx.measureText(line).width;
                const startX = subCenterX - lineW / 2;

                if (ribbonEnabled && li <= activeLineIdx) {
                    let coveredW = lineW;
                    if (li === activeLineIdx) {
                        const lineWords = line.trim().split(/\s+/);
                        const coveredPrefix = lineWords.slice(0, activeWordInLine + 1).join(' ');
                        coveredW = ctx.measureText(coveredPrefix).width;
                    }
                    const padX = 12 * scale;
                    const padY = 4 * scale;
                    ctx.save();
                    ctx.shadowColor = 'transparent';
                    ctx.globalAlpha = ribbonAlpha;
                    ctx.fillStyle = ribbonColor;
                    this.drawRoundedRect(ctx, startX - padX, ly - fontSize / 2 - padY, coveredW + padX * 2, fontSize + padY * 2, 2 * scale);
                    ctx.fill();
                    ctx.restore();
                }

                ctx.textAlign = 'center';
                if (preset.use_stroke && preset.border_width > 0) {
                    ctx.strokeStyle = preset.color_outline || '#000000';
                    ctx.lineWidth = Math.max(2, preset.border_width * scale * 2);
                    ctx.lineJoin = 'round';
                    ctx.strokeText(line, subCenterX, ly);
                }
                ctx.save();
                ctx.globalAlpha = textAlpha;
                ctx.fillStyle = preset.color_text || '#FFFFFF';
                ctx.fillText(line, subCenterX, ly);
                ctx.restore();
            }

            ctx.restore();
            return;
        }

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
                    if (preset.use_stroke && preset.border_width > 0) {
                        ctx.strokeStyle = preset.color_outline || '#6488A8';
                        ctx.lineWidth = Math.max(1, preset.border_width * scale * 2);
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
                ctx.lineWidth = Math.max(2, (preset.border_width || 5) * scale * 2);
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

    drawFollowerCursor(ctx, x, y, fontSize, cursorStyle, scale) {
        ctx.save();
        ctx.shadowColor = 'transparent';

        if (cursorStyle === 1) {
            // 🌺 1:1 大号卡通四瓣红花 + 绿茎绿叶 + 黑金花蕊
            const u = Math.max(16, fontSize * 0.62);
            // 1. 绿茎 + 绿叶
            ctx.strokeStyle = '#184A14';
            ctx.lineWidth = Math.max(2, u * 0.16);
            ctx.lineCap = 'round';
            ctx.beginPath();
            ctx.moveTo(x - u * 0.12, y + u * 0.18);
            ctx.lineTo(x - u * 0.88, y + u * 1.05);
            ctx.stroke();
            ctx.strokeStyle = '#2E9E43';
            ctx.lineWidth = Math.max(1.5, u * 0.10);
            ctx.stroke();

            ctx.fillStyle = '#34C759';
            ctx.strokeStyle = '#144D1E';
            ctx.lineWidth = Math.max(1.5, 2 * scale);
            ctx.beginPath();
            ctx.ellipse(x - u * 0.56, y + u * 0.52, u * 0.28, u * 0.15, -0.9, 0, Math.PI * 2);
            ctx.fill();
            ctx.stroke();

            // 2. 四片饱满鲜红花瓣 + 深黑描边
            const pr = u * 0.46;
            const po = u * 0.44;
            const angles = [-82, 12, 100, -168];
            ctx.fillStyle = '#E62620';
            ctx.strokeStyle = '#1A0D0D';
            ctx.lineWidth = Math.max(1.8, 3 * scale);
            for (const deg of angles) {
                const rad = (deg * Math.PI) / 180;
                ctx.beginPath();
                ctx.arc(x + Math.cos(rad) * po, y + Math.sin(rad) * po, pr, 0, Math.PI * 2);
                ctx.fill();
                ctx.stroke();
            }

            // 3. 三层同心花蕊 (黑 -> 金黄 -> 黑)
            ctx.fillStyle = '#1A0D0D';
            ctx.beginPath();
            ctx.arc(x, y, u * 0.24, 0, Math.PI * 2);
            ctx.fill();
            ctx.fillStyle = '#FFD833';
            ctx.beginPath();
            ctx.arc(x, y, u * 0.17, 0, Math.PI * 2);
            ctx.fill();
            ctx.fillStyle = '#1A0D0D';
            ctx.beginPath();
            ctx.arc(x, y, u * 0.09, 0, Math.PI * 2);
            ctx.fill();
        } else if (cursorStyle === 2) {
            // 🖋️ 1:1 纤细斜执黑金荧光笔 (笔尖在 (x, y)，向右上 45° 纤细延伸)
            const u = Math.max(20, fontSize * 0.88);
            ctx.translate(x, y);
            ctx.rotate(-Math.PI / 4);
            // 笔尖发光金黄
            ctx.fillStyle = '#FFE033';
            ctx.beginPath();
            ctx.moveTo(0, 0);
            ctx.lineTo(u * 0.34, -u * 0.11);
            ctx.lineTo(u * 0.34, u * 0.11);
            ctx.closePath();
            ctx.fill();
            // 纤细黑色笔杆 + 金色细边框
            ctx.fillStyle = '#1E1B24';
            ctx.strokeStyle = '#F5C542';
            ctx.lineWidth = Math.max(1.5, 2.5 * scale);
            this.drawRoundedRect(ctx, u * 0.30, -u * 0.11, u * 1.65, u * 0.22, u * 0.10);
            ctx.fill();
            ctx.stroke();
        } else if (cursorStyle === 3) {
            // ✨ 圣光四角星芒
            const s = Math.max(14, fontSize * 0.48);
            ctx.fillStyle = '#FFE866';
            const outer = s * 0.78;
            const inner = s * 0.22;
            ctx.beginPath();
            for (let i = 0; i < 8; i++) {
                const rad = i % 2 === 0 ? outer : inner;
                const ang = (i * Math.PI) / 4;
                const px = x + Math.cos(ang) * rad;
                const py = y + Math.sin(ang) * rad;
                if (i === 0) ctx.moveTo(px, py);
                else ctx.lineTo(px, py);
            }
            ctx.closePath();
            ctx.fill();
        }
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
            vctx.font = `bold ${fontSize}px "${this.state.watermarkFontFamily || 'Arial'}", Arial, sans-serif`;
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
