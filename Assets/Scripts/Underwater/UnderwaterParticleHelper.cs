using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Utility helper to generate procedural soft glowing particle textures and materials.
    /// Guarantees that ParticleSystemRenderer never has a null material, completely
    /// eliminating magenta/pink untextured squares across all platforms and render pipelines (URP).
    /// </summary>
    public static class UnderwaterParticleHelper
    {
        private static Texture2D cachedParticleTex;

        public static Texture2D GetOrCreateSoftCircleTexture()
        {
            if (cachedParticleTex != null) return cachedParticleTex;

            int size = 32;
            cachedParticleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            cachedParticleTex.name = "SoftParticleRadialAlpha";
            cachedParticleTex.wrapMode = TextureWrapMode.Clamp;
            cachedParticleTex.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = Mathf.SmoothStep(0f, 1f, alpha);
                    alpha = alpha * alpha; // Quadratic soft glow falloff
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }
            cachedParticleTex.SetPixels(pixels);
            cachedParticleTex.Apply();
            return cachedParticleTex;
        }

        public static Material CreateParticleMaterial(Color color, bool additive = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            Material mat = new Material(shader);
            mat.name = "ProceduralParticleMaterial";
            Texture2D tex = GetOrCreateSoftCircleTexture();

            mat.mainTexture = tex;
            mat.color = color;

            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // Transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", additive ? 1f : 0f);

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            if (additive) mat.EnableKeyword("_BLENDMODE_ADD");
            else mat.EnableKeyword("_BLENDMODE_ALPHA");

            mat.renderQueue = 3000;
            return mat;
        }

        public static void ApplyParticleMaterial(ParticleSystem ps, Color color, bool additive = false)
        {
            if (ps == null) return;
            var ren = ps.GetComponent<ParticleSystemRenderer>();
            if (ren != null)
            {
                ren.renderMode = ParticleSystemRenderMode.Billboard;
                ren.sharedMaterial = CreateParticleMaterial(color, additive);
            }
        }
    }
}

