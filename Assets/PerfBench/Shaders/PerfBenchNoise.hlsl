#ifndef PERFBENCH_NOISE_INCLUDED
#define PERFBENCH_NOISE_INCLUDED

// Globais definidas pelo C# (Shader.SetGlobalFloat). Globais não quebram o SRP Batcher.
float _PB_AluIterations;
float _PB_OverdrawAlu;

// Hash sem seno (Dave Hoskins): estável em precisão total.
float PB_Hash13(float3 p3)
{
    p3 = frac(p3 * 0.1031);
    p3 += dot(p3, p3.zyx + 31.32);
    return frac((p3.x + p3.y) * p3.z);
}

float PB_ValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float a = PB_Hash13(i);
    float b = PB_Hash13(i + float3(1, 0, 0));
    float c = PB_Hash13(i + float3(0, 1, 0));
    float d = PB_Hash13(i + float3(1, 1, 0));
    float e = PB_Hash13(i + float3(0, 0, 1));
    float g = PB_Hash13(i + float3(1, 0, 1));
    float h = PB_Hash13(i + float3(0, 1, 1));
    float k = PB_Hash13(i + float3(1, 1, 1));
    return lerp(lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y),
                lerp(lerp(e, g, u.x), lerp(h, k, u.x), u.y), u.z);
}

// Carga de ALU calibrável: custo linear no número de iterações.
// O contador vem de uma uniform, então o driver não consegue desenrolar nem eliminar
// o laço; o resultado entra na cor final, então também não é código morto.
float PB_StressNoise(float3 p, int iterations)
{
    if (iterations <= 0) return 0.5;
    float sum = 0.0;
    float wsum = 0.0;
    [loop] for (int i = 0; i < iterations; i++)
    {
        float w = rcp(1.0 + i);
        sum += PB_ValueNoise(p) * w;
        wsum += w;
        p = p.yzx * 1.73 + float3(3.1, 1.7, 5.3);
        p = frac(p * (1.0 / 61.0)) * 61.0; // mantém p limitado (sem overflow)
    }
    return sum / wsum;
}

half3 PB_Palette(float t)
{
    return half3(0.5 + 0.5 * cos(6.2831853 * (t + float3(0.0, 0.33, 0.67))));
}

#endif
