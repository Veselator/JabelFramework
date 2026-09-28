// Universal monkey shader for "1000 Monkeys".
// Every monkey level enables its own combination of effects through the _FxMask bit field.
// One shader variant, uniform branches: all levels batch-friendly and no keyword explosion.
// Sprite tint comes from the SpriteRenderer color (unity_SpriteColor), so each monkey keeps its random color.
Shader "OneKMonkeys/MonkeyEffects"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _FxMask ("Effect Mask (bits)", Float) = 0
        _SpriteRect ("Sprite UV Rect (xy = min, zw = size)", Vector) = (0, 0, 1, 1)
        _SheetTexel ("Sheet Texel Size (1/w, 1/h)", Vector) = (0.00048828125, 0.00048828125, 0, 0)
        _OutlineColor ("Outline Color", Color) = (0.05, 0.05, 0.08, 1)
        _GlowColor ("Glow Color", Color) = (0.4, 0.9, 1, 1)
        _AccentColor ("Accent Color", Color) = (1, 0.85, 0.3, 1)
        _OutlineWidth ("Outline Width (texels)", Float) = 2.5
        _GlowWidth ("Glow Width (texels)", Float) = 7
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    // Effect bits (must match OneKMonkeys.MonkeyFx).
    #define FX_OUTLINE          (1u << 0)
    #define FX_GLOW             (1u << 1)
    #define FX_SHINE            (1u << 2)
    #define FX_PULSE            (1u << 3)
    #define FX_WOBBLE           (1u << 4)
    #define FX_HOLOGRAM         (1u << 5)
    #define FX_RAINBOW          (1u << 6)
    #define FX_SPARKLES         (1u << 7)
    #define FX_GOLD             (1u << 8)
    #define FX_GLITCH           (1u << 9)
    #define FX_CHROMATIC        (1u << 10)
    #define FX_ELECTRIC         (1u << 11)
    #define FX_FIRE             (1u << 12)
    #define FX_GHOST            (1u << 13)
    #define FX_GALAXY           (1u << 14)
    #define FX_DISSOLVE         (1u << 15)
    #define FX_RAINBOW_OUTLINE  (1u << 16)
    #define FX_HALO             (1u << 17)
    #define FX_INNER_GLOW       (1u << 18)

    TEXTURE2D(_MainTex);
    SAMPLER(sampler_MainTex);

    CBUFFER_START(UnityPerMaterial)
        // Texel size of the sprite sheet. Set explicitly: with the SRP batcher, _SheetTexel
        // would come from the material (empty), not from the SpriteRenderer's texture.
        float4 _SheetTexel;
        float _FxMask;
        float4 _SpriteRect;
        float4 _OutlineColor;
        float4 _GlowColor;
        float4 _AccentColor;
        float _OutlineWidth;
        float _GlowWidth;
    CBUFFER_END

    struct Attributes
    {
        float3 positionOS : POSITION;
        float4 color : COLOR;
        float2 uv : TEXCOORD0;
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        float4 color : COLOR;
        float2 uv : TEXCOORD0;
        float3 positionWS : TEXCOORD1;
    };

    float Hash21(float2 p)
    {
        p = frac(p * float2(123.34, 456.21));
        p += dot(p, p + 45.32);
        return frac(p.x * p.y);
    }

    float Noise(float2 p)
    {
        float2 i = floor(p);
        float2 f = frac(p);
        float a = Hash21(i);
        float b = Hash21(i + float2(1, 0));
        float c = Hash21(i + float2(0, 1));
        float d = Hash21(i + float2(1, 1));
        float2 u = f * f * (3 - 2 * f);
        return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
    }

    float3 HueShift(float3 color, float shift)
    {
        // Rodrigues rotation around the grey axis.
        const float3 k = float3(0.57735, 0.57735, 0.57735);
        float angle = shift * 6.28318;
        float c = cos(angle);
        return color * c + cross(k, color) * sin(angle) + k * dot(k, color) * (1 - c);
    }

    float3 Rainbow(float t)
    {
        return saturate(abs(frac(t + float3(0, 0.333, 0.666)) * 6 - 3) - 1);
    }

    float SampleAlpha(float2 uv)
    {
        return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
    }

    // Max alpha of neighbours at a given radius (8 directions).
    float NeighbourMax(float2 uv, float radius)
    {
        float2 t = _SheetTexel.xy * radius;
        float m = 0;
        m = max(m, SampleAlpha(uv + float2(t.x, 0)));
        m = max(m, SampleAlpha(uv - float2(t.x, 0)));
        m = max(m, SampleAlpha(uv + float2(0, t.y)));
        m = max(m, SampleAlpha(uv - float2(0, t.y)));
        m = max(m, SampleAlpha(uv + t * 0.707));
        m = max(m, SampleAlpha(uv - t * 0.707));
        m = max(m, SampleAlpha(uv + float2(t.x, -t.y) * 0.707));
        m = max(m, SampleAlpha(uv + float2(-t.x, t.y) * 0.707));
        return m;
    }

    float NeighbourMin(float2 uv, float radius)
    {
        float2 t = _SheetTexel.xy * radius;
        float m = 1;
        m = min(m, SampleAlpha(uv + float2(t.x, 0)));
        m = min(m, SampleAlpha(uv - float2(t.x, 0)));
        m = min(m, SampleAlpha(uv + float2(0, t.y)));
        m = min(m, SampleAlpha(uv - float2(0, t.y)));
        return m;
    }

    Varyings Vert(Attributes input)
    {
        Varyings o;
        uint fx = (uint)_FxMask;
        float3 pos = input.positionOS;
        float t = _Time.y;

        if (fx & FX_WOBBLE)
        {
            // Jelly wobble: horizontal wave travelling up the body.
            pos.x += sin(pos.y * 4.0 + t * 5.0) * 0.045;
            pos.y += sin(pos.x * 3.0 + t * 3.0) * 0.02;
        }

        o.positionWS = TransformObjectToWorld(pos);
        o.positionCS = TransformWorldToHClip(o.positionWS);
        // URP passes SpriteRenderer.color through unity_SpriteColor (vertex colors stay white).
        o.color = input.color * unity_SpriteColor;
        o.uv = input.uv;
        return o;
    }

    float4 Frag(Varyings i) : SV_Target
    {
        uint fx = (uint)_FxMask;
        float t = _Time.y;
        float2 uv = i.uv;
        float2 local = (uv - _SpriteRect.xy) / max(_SpriteRect.zw, 1e-5);
        float2 world = i.positionWS.xy;

        // ---- UV distortions
        if (fx & FX_GLITCH)
        {
            float band = floor(local.y * 22.0);
            float frame = floor(t * 9.0);
            float trigger = step(0.86, Hash21(float2(band, frame)));
            uv.x += (Hash21(float2(band * 1.7, frame)) - 0.5) * 0.06 * _SpriteRect.z * trigger;
        }

        float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

        if (fx & FX_CHROMATIC)
        {
            float2 off = float2(_SheetTexel.x * (2.5 + 1.5 * sin(t * 3.0)), 0);
            float4 r = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + off);
            float4 b = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - off);
            tex.r = r.r;
            tex.b = b.b;
            tex.a = max(tex.a, max(r.a, b.a));
        }

        float alpha = tex.a;
        float lum = dot(tex.rgb, float3(0.299, 0.587, 0.114));
        float3 col = tex.rgb * i.color.rgb;

        // ---- Colour treatments
        if (fx & FX_GOLD)
        {
            float3 gold = lerp(float3(0.45, 0.28, 0.04), float3(1.0, 0.88, 0.42), lum);
            float sheen = pow(saturate(sin(local.x * 7.0 + local.y * 3.0 - t * 2.0) * 0.5 + 0.5), 8.0);
            col = lerp(col, gold * (0.35 + lum), 0.85) + sheen * 0.35 * lum;
        }

        if (fx & FX_RAINBOW)
        {
            col = HueShift(col, t * 0.35 + local.x * 0.5);
            col = lerp(col, Rainbow(t * 0.3 + local.y) * lum, 0.35);
        }

        if (fx & FX_HOLOGRAM)
        {
            float scan = 0.7 + 0.3 * sin(world.y * 70.0 - t * 8.0);
            float3 holo = float3(0.25, 1.0, 0.65) * (0.3 + lum);
            col = lerp(col, holo, 0.55) * scan;
            alpha *= 0.8 + 0.2 * sin(t * 23.0);
        }

        if (fx & FX_GALAXY)
        {
            // Bright fill areas turn into a moving starfield.
            float fill = smoothstep(0.55, 0.8, lum);
            float2 p = world * 9.0 + float2(t * 0.15, t * 0.05);
            float3 space = lerp(float3(0.08, 0.02, 0.2), float3(0.25, 0.05, 0.45), Noise(p * 0.35));
            float2 cell = floor(p * 3.0);
            float star = step(0.93, Hash21(cell)) * pow(saturate(sin(t * 3.0 + Hash21(cell + 7.0) * 40.0)), 4.0);
            float2 f = frac(p * 3.0) - 0.5;
            star *= saturate(1.0 - length(f) * 4.0);
            col = lerp(col, space + star * float3(1.0, 0.95, 0.8) * 2.0, fill);
        }

        if (fx & FX_PULSE)
        {
            col *= 1.0 + 0.3 * sin(t * 4.5);
        }

        if (fx & FX_SHINE)
        {
            float band = local.x + local.y * 0.6 - (frac(t * 0.35) * 3.2 - 0.8);
            float shine = smoothstep(0.12, 0.0, abs(band));
            col += shine * 0.8 * alpha;
        }

        if (fx & FX_SPARKLES)
        {
            float2 p = world * 12.0;
            float2 cell = floor(p);
            float h = Hash21(cell);
            float2 f = frac(p) - 0.5;
            float sparkShape = saturate(1.0 - abs(f.x) * 14.0) * saturate(1.0 - abs(f.y) * 3.0) +
                          saturate(1.0 - abs(f.y) * 14.0) * saturate(1.0 - abs(f.x) * 3.0);
            float twinkle = step(0.8, h) * pow(saturate(sin(t * 4.0 + h * 60.0)), 6.0);
            col += sparkShape * twinkle * 1.8 * alpha;
        }

        if (fx & FX_DISSOLVE)
        {
            float n = Noise(world * 11.0 + t * 0.4);
            float threshold = 0.35 + 0.15 * sin(t * 1.3);
            float edge = smoothstep(0.06, 0.0, abs(n - threshold));
            alpha *= lerp(0.35, 1.0, step(threshold, n));
            col += edge * _GlowColor.rgb * 1.5;
            alpha = max(alpha, edge * tex.a);
        }

        if (fx & FX_INNER_GLOW)
        {
            float inner = alpha * (1.0 - NeighbourMin(uv, 4.0));
            col += inner * _AccentColor.rgb * (0.8 + 0.4 * sin(t * 3.0));
        }

        // ---- Effects outside the silhouette (composited behind the sprite)
        float3 backColor = 0;
        float backAlpha = 0;

        if (fx & FX_GHOST)
        {
            float2 o1 = float2(sin(t * 2.0), cos(t * 1.6)) * _SheetTexel.xy * 9.0;
            float ghost = max(SampleAlpha(uv + o1), SampleAlpha(uv - o1 * 1.6)) * 0.45;
            backColor = lerp(backColor, float3(0.6, 0.75, 1.0), ghost);
            backAlpha = max(backAlpha, ghost);
        }

        if (fx & FX_FIRE)
        {
            // Flames rise from any solid pixel below.
            float below = 0;
            [unroll] for (int k = 1; k <= 4; k++)
                below = max(below, SampleAlpha(uv - float2(0, _SheetTexel.y * k * 3.0)) * (1.0 - k * 0.2));
            float n = Noise(world * float2(9.0, 5.0) - float2(0, t * 3.5));
            float flame = saturate(below * (n * 1.6 - 0.2));
            float3 fireColor = lerp(float3(1.0, 0.25, 0.0), float3(1.0, 0.9, 0.3), n);
            backColor = lerp(backColor, fireColor, flame);
            backAlpha = max(backAlpha, flame);
            col += (1.0 - lum) * 0.15 * float3(1.0, 0.4, 0.0);
        }

        if (fx & FX_GLOW)
        {
            float glow = NeighbourMax(uv, _GlowWidth) * 0.55 + NeighbourMax(uv, _GlowWidth * 0.5) * 0.45;
            glow *= 0.75 + 0.25 * sin(t * 2.5);
            backColor = lerp(backColor, _GlowColor.rgb, glow);
            backAlpha = max(backAlpha, glow * 0.8);
        }

        if (fx & FX_HALO)
        {
            float2 d = (local - float2(0.43, 0.83)) / float2(0.2, 0.055);
            float ring = smoothstep(0.35, 0.0, abs(length(d) - 1.0));
            float3 haloColor = float3(1.0, 0.9, 0.45) * (1.2 + 0.3 * sin(t * 3.0));
            backColor = lerp(backColor, haloColor, ring);
            backAlpha = max(backAlpha, ring);
        }

        if (fx & (FX_OUTLINE | FX_ELECTRIC | FX_RAINBOW_OUTLINE))
        {
            float width = _OutlineWidth;
            if (fx & FX_ELECTRIC) width += Noise(world * 25.0 + t * 12.0) * 3.0;
            float outline = saturate(NeighbourMax(uv, width) - alpha);
            float3 outlineColor = _OutlineColor.rgb;
            if (fx & FX_ELECTRIC)
                outlineColor = lerp(float3(0.3, 0.7, 1.0), float3(1, 1, 1), step(0.7, Noise(world * 40.0 + t * 20.0)));
            if (fx & FX_RAINBOW_OUTLINE)
                outlineColor = Rainbow(t * 0.5 + (world.x + world.y) * 0.8);
            backColor = lerp(backColor, outlineColor, outline);
            backAlpha = max(backAlpha, outline);
        }

        // Composite: sprite over background effects.
        float3 rgb = lerp(backColor, col, alpha);
        float a = alpha + backAlpha * (1.0 - alpha);
        return float4(rgb, a * i.color.a);
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Universal2D"
            Tags { "LightMode" = "Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}
