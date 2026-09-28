using System.Collections.Generic;
using UnityEngine;

namespace OneKMonkeys
{
    /// <summary>
    /// One shared material per effect combination: all monkeys of the same level share a material,
    /// their individual colors travel through SpriteRenderer.color, so batching stays intact.
    /// </summary>
    public sealed class MonkeyMaterials
    {
        private static readonly int FxMask = Shader.PropertyToID("_FxMask");
        private static readonly int SpriteRect = Shader.PropertyToID("_SpriteRect");
        private static readonly int GlowColor = Shader.PropertyToID("_GlowColor");
        private static readonly int SheetTexel = Shader.PropertyToID("_SheetTexel");

        private readonly Material _template;
        private readonly Vector4 _spriteRect;
        private readonly Vector4 _texel = new Vector4(1f / 2048, 1f / 2048, 0, 0);
        private readonly Dictionary<(int, Color), Material> _cache = new Dictionary<(int, Color), Material>();

        public MonkeyMaterials(Material template, Sprite bodySprite)
        {
            _template = template;
            if (bodySprite != null && bodySprite.texture != null)
            {
                var r = bodySprite.textureRect;
                var tex = bodySprite.texture;
                _spriteRect = new Vector4(r.x / tex.width, r.y / tex.height, r.width / tex.width, r.height / tex.height);
                _texel = new Vector4(1f / tex.width, 1f / tex.height, 0, 0);
            }
            else _spriteRect = new Vector4(0, 0, 1, 1);
        }

        public Material Get(MonkeyFx effects, Color glow)
        {
            var key = ((int)effects, glow);
            if (_cache.TryGetValue(key, out var material)) return material;

            material = new Material(_template) { name = $"Monkey_{effects}" };
            material.SetFloat(FxMask, (int)effects);
            material.SetVector(SpriteRect, _spriteRect);
            material.SetColor(GlowColor, glow);
            material.SetVector(SheetTexel, _texel);
            _cache[key] = material;
            return material;
        }
    }
}
