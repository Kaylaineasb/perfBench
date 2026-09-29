# PerfBench: jogo-carga (Unity 6.3 / URP / Android)

App de carga determinística para o benchmark de celulares. Tem três cenários (LEVE, MÉDIO, PESADO) executados em blocos. Os parâmetros ficam em ScriptableObjects, as métricas são gravadas em CSV/JSON e o app pode ser controlado por deeplink.

## 1. Instalação no projeto

1. **Faça um commit/backup do projeto.**
2. Copie a pasta `PerfBench/` inteira para dentro de `Assets/`.
3. No Package Manager, confira se estes pacotes estão instalados. Normalmente já vêm como dependência do URP 17:
   - **Burst** (`com.unity.burst`)
   - **Collections** (`com.unity.collections`)
   - **Mathematics** (`com.unity.mathematics`)
4. Com a cena `game` aberta, rode o menu **PerfBench → 1. Configurar projeto e cena aberta**. Esse passo:
   - cria materiais, perfis `Scenario_light/medium/heavy` e o `BenchmarkConfig` em `Assets/PerfBench/Generated/`;
   - cria o objeto `PerfBench` com todos os módulos, já ligados à câmera, à luz e ao `player`;
   - desativa `ObstacleSpawner`, `pista` e o `Global Volume` antigo, porque o jogo cria os próprios;
   - ajusta o URP: Forward+, luzes adicionais por pixel, sombras adicionais e soft, GPU Resident Drawer desligado;
   - ajusta o Player: Frame Timing Stats ligado, retrato, IL2CPP, ARM64, Optimized Frame Pacing desligado, sem Development Build.
5. Salve a cena (Ctrl+S) e dê Play. No editor o overlay aparece e os arquivos vão para `Application.persistentDataPath/perfbench/`.
6. Leia o Console: qualquer ajuste que não pôde ser feito automaticamente aparece como aviso, e aí é só fazer à mão.

> O Package Name ainda é o do template (`com.UnityTechnologies...urpblank`). Defina um id próprio em Player Settings → Identification, por exemplo `br.com.suaempresa.perfbench`.

## 2. Estrutura

```
PerfBench/
  Scripts/Config    ScenarioProfile (parâmetros por cenário), BenchmarkConfig
  Scripts/Core      BenchmarkRunner, StageTimeline, DeterministicRandom, RunOptions (deeplink), sinais
  Scripts/Game      WorldSimulation (pista, obstáculos, bot, câmera)
  Scripts/Load      um módulo por alavanca de carga
  Scripts/Metrics   MetricsRecorder (CSV/JSON/overlay), BatteryProbe (JNI)
  Shaders           StressLit (PBR + loop ALU), Overdraw, Particle
  Editor            menu de setup, patch do AndroidManifest (deeplink)
```

| Módulo (id) | O que carrega |
|---|---|
| `world` | O jogo em si: pista, obstáculos com seed e bot. Não pode ser desligado. |
| `render` | Resolução fixa em pixels, MSAA, HDR, sombras, loop de shader, pós-processamento, teto de FPS. Sub-ids: `resolution`, `msaa`, `shadows`, `shader`, `post`. |
| `instanced` | Milhares de objetos com GPU instancing; matrizes calculadas em job Burst. |
| `objects` | Objetos individuais sem batching (MaterialPropertyBlock), piscando e voando. |
| `overdraw` | Camadas transparentes em tela cheia (fillrate). |
| `particles` | Partículas aditivas. |
| `lights` | Luzes pontuais e spots com sombra (Forward+). |
| `vcam` | Câmera virtual: render offscreen, AsyncGPUReadback e filtro Sobel em job Burst. |
| `cpu` | Job Burst em todas as threads de worker, sem bloquear a thread principal. |
| `physics` | Esferas com colisão num contêiner, recebendo impulsos determinísticos. |
| `gc` | Alocações de vida curta a uma taxa fixa em MB/s. |

## 3. Execução

Sem deeplink (toque no ícone), o app usa os padrões do `BenchmarkConfig`: light → medium → heavy, 180 s cada, seed 42, com overlay.

Com deeplink, controlado pelo sistema do parceiro:

```bash
adb shell am start -W -a android.intent.action.VIEW \
  -d "perfbench://start?seq=light,medium,heavy&stage=180&seed=42"
```

| Parâmetro | Exemplo | Efeito |
|---|---|---|
| `seq` (ou `scenario`) | `light,medium,heavy` | Ordem dos blocos (pode repetir) |
| `stage` (ou `duration`) | `180` | Segundos por bloco |
| `seed` | `42` | Seed da geração procedural |
| `loops` | `2` | Repete a sequência inteira |
| `overlay` | `1` | Mostra métricas na tela (padrão 0 via deeplink) |
| `autoquit` | `0` | Não fecha ao terminar (padrão 1 via deeplink) |
| `fps` | `60` | Teto de FPS manual (padrão = refresh máximo do painel) |
| `off` | `shadows,post` | Desliga módulos, para a calibração |

Sinais de progresso e fim:

```bash
adb logcat -s PERFBENCH
# START run=20260928_193000_seed42 seq=light,medium,heavy stage=180 seed=42 out=...
# STAGE index=1 id=medium name=MÉDIO
# END run=... status=finished out=/storage/emulated/0/Android/data/<pkg>/files/perfbench/<runId>
```

Também é gravado `perfbench/status.json` com `state` = `running`, `finished` ou `aborted`. Ao terminar, o app fecha sozinho.

## 4. Arquivos gerados

```bash
adb pull /storage/emulated/0/Android/data/<pkg>/files/perfbench/ ./resultados/
```

- `<runId>/metrics.csv`: uma linha por segundo (FPS, frame time de CPU e GPU, draw calls, triângulos, memória, GC, bateria em µA/µAh, temperatura, estado térmico, carregador conectado).
- `<runId>/summary.json`: informações do aparelho e estatísticas por bloco (FPS médio, 1% low, percentis, CPU/GPU ms, Δ carga em µAh, temperatura máxima, hash do mundo).

## 5. Build de release (checklist)

- [ ] Build Settings → Android, cena `game` na lista
- [ ] **Development Build desmarcado**, Autoconnect Profiler desmarcado
- [ ] Scripting Backend **IL2CPP**; Target Architectures **somente ARM64**
- [ ] IL2CPP Code Generation: *Optimize for runtime speed*; C++ Compiler Configuration: *Release*
- [ ] Frame Timing Stats **ligado**
- [ ] Optimized Frame Pacing **desligado**
- [ ] Graphics APIs: **Vulkan** primeiro (deixe OpenGLES3 como fallback só se precisar de aparelhos antigos, e registre qual API rodou: vem no `summary.json`)
- [ ] Quality: o nível usado no Android aponta para o URP Asset ajustado (Forward+)
- [ ] Package Name próprio; versão (`Version`/`Bundle Version Code`) incrementada a cada build usado em teste
- [ ] Não instale o pacote **Adaptive Performance** (ele reduziria a carga sozinho)
- [ ] Gere o APK e instale com `adb install -r perfbench.apk`

A mesma versão do APK deve ser usada em todos os aparelhos de uma comparação. Mudou algum parâmetro? Gere nova versão e registre.
