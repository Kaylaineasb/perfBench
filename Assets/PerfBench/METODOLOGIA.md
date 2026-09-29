# Metodologia: jogo-carga PerfBench

## 1. Objetivo

O PerfBench é uma **carga de trabalho padronizada** para o benchmark de celulares. Ele sustenta, por um tempo definido, um uso intenso e reproduzível de **GPU, CPU e memória**, como faz um jogo 3D. Assim, o sistema de benchmark consegue medir o **consumo de bateria**, o **aquecimento** e o **desempenho sustentado** de cada aparelho sob a mesma exigência.

## 2. Princípio: mesma cena, FPS livre

A comparação entre aparelhos se apoia em uma regra: **o conteúdo de cada frame é idêntico em todos os aparelhos**. Isso inclui a mesma geometria, as mesmas luzes, a mesma resolução interna em pixels e os mesmos efeitos. O que varia é **quantos frames por segundo** cada aparelho consegue produzir. O FPS não é configuração: é resultado.

Consequências:

- Um aparelho mais potente renderiza mais frames. Com isso, trabalha mais, consome mais energia e aquece até o limite do próprio projeto térmico. É o que se quer medir.
- Um aparelho mais fraco roda com FPS menor no mesmo cenário. Esse FPS menor é registrado como resultado.
- O teto de FPS é o **refresh máximo do painel** (60, 90, 120 Hz...). No Android não existe FPS acima do refresh, porque o compositor sempre sincroniza com a tela. Por isso toda a escala de carga vem do **custo por frame**, não da quantidade de frames.

Essa é a mesma abordagem de testes de estresse como o 3DMark Wild Life Stress Test.

## 3. Determinismo

Para a carga ser reproduzível, o jogo não usa nenhuma aleatoriedade sem seed e nenhuma lógica que dependa do tempo de frame:

| Elemento | Como é garantido |
|---|---|
| Sequência de obstáculos | Gerador próprio (SplitMix64) com seed. A mesma seed produz a mesma pista em qualquer aparelho. |
| Posição do mundo | Distância percorrida = função analítica do tempo (velocidade × duração de cada bloco). No instante *t*, o mundo está na mesma posição em qualquer FPS. |
| Decisão do bot (trocar de faixa, pular) | Calculada junto com cada fileira de obstáculos, no domínio da distância, sem raycast por frame. |
| Objetos animados, luzes, campo instanciado | Posição = f(seed, índice, tempo). |
| Física | Passo fixo, impulsos determinísticos a cada 25 passos, sleep desligado. |
| Partículas | Seed fixa e taxa de emissão por segundo. A quantidade em tela é **equivalente** entre aparelhos, mas não idêntica bit a bit. |

**Comprovação:** ao fim de cada bloco, o `summary.json` registra o `worldHash`, um hash de todas as fileiras de obstáculos geradas até ali. Com a mesma seed, a mesma sequência e a mesma duração, o hash precisa ser igual em todos os aparelhos.

## 4. Cenários e parâmetros

A execução é uma sequência de **blocos** (padrão: LEVE → MÉDIO → PESADO, 180 s cada). Ordem, duração e seed são configuráveis por deeplink. Os primeiros **3 s** de cada bloco são excluídos das estatísticas (aquecimento), porque a troca de resolução e de MSAA causa travadas pontuais.

Valores iniciais, a serem ajustados na calibração (Seção 7):

| Alavanca | Parâmetro | LEVE | MÉDIO | PESADO | Recurso estressado |
|---|---|---|---|---|---|
| Jogo | Velocidade (m/s) / espaçamento (m) | 15 / 30 | 25 / 15 | 35 / 10 | — |
| Resolução | Pixels renderizados | 0,9 MP | 2,1 MP | 3,7 MP | GPU (fillrate, banda) |
| | MSAA / HDR | 1× / não | 2× / sim | 4× / sim | GPU (banda de memória) |
| Shader | Iterações do loop de ruído por pixel | 4 | 32 | 128 | GPU (ALU) |
| Geometria | Objetos com instancing (~480 tri) | 300 | 2.000 | 10.000 | GPU (vértices) + CPU (job) |
| | Objetos sem batching | 20 | 200 | 800 | CPU (draw calls, render thread) |
| Fillrate | Camadas de overdraw em tela cheia | 0 | 4 | 12 | GPU (fillrate, blending) |
| | Partículas (máx.) | 0 | 2.000 | 10.000 | CPU (simulação) + GPU |
| Luzes | Pontuais / spots com sombra | 0 / 0 | 8 / 0 | 28 / 4 | GPU (Forward+) |
| | Sombra principal (resolução, cascatas) | 1024, 1, hard | 2048, 2, soft | 4096, 4, soft | GPU (passes de sombra) |
| Pós | Efeitos | — | Bloom, cor | Bloom HQ, DoF Bokeh, Motion Blur, cor | GPU |
| Câmera virtual | Render offscreen + readback + Sobel | — | 1024², a cada 4 frames | 2048², todo frame | GPU + banda + CPU |
| CPU | Elementos × iterações no job Burst | 0 | 65.536 × 64 | 262.144 × 64 | CPU (todas as threads) |
| | Corpos físicos | 0 | 150 | 600 | CPU (PhysX) |
| | Pressão de GC | 0 | 2 MB/s | 8 MB/s | CPU + memória |

A **resolução interna é fixada em pixels absolutos**, e não como porcentagem da tela. O render scale é calculado a partir da tela de cada aparelho, de modo que todos renderizem a mesma quantidade de pixels. A resolução efetiva aparece no CSV (`render_w`, `render_h`).

Recursos adaptativos ficam **desligados** de propósito (Adaptive Performance, GPU Resident Drawer, Optimized Frame Pacing), porque mudariam a carga por conta própria.

## 5. O que é medido

### 5.1 Pelo próprio app (arquivos no aparelho)

**`metrics.csv`: uma linha por segundo**

| Grupo | Colunas | Fonte |
|---|---|---|
| Contexto | `utc_ms`, `elapsed_s`, `stage_id`, `stage_elapsed_s`, `speed_mps`, `distance_m` | App |
| Fluidez | `fps_avg`, `fps_min_frame`, `fps_max_frame`, `frame_ms_avg`, `frame_ms_max` | Tempo entre frames |
| CPU/GPU | `cpu_frame_ms`, `cpu_main_ms`, `cpu_render_ms`, `gpu_frame_ms` | `FrameTimingManager` |
| Carga de render | `draw_calls`, `batches`, `setpass`, `triangles`, `vertices`, `active_objects`, `particles` | `ProfilerRecorder` / app |
| Memória | `total_mem_mb`, `gc_used_mb`, `gc_alloc_frame_kb`, `gc_collections` | `ProfilerRecorder`, `GC` |
| Energia | `battery_pct`, `current_now_raw`, `charge_counter_uah`, `voltage_mv`, `plugged` | `BatteryManager` |
| Térmico | `battery_temp_c`, `thermal_status`, `thermal_headroom` | Intent de bateria, `PowerManager` |
| Render | `render_w`, `render_h`, `target_fps` | App |

Valor `-1` ou vazio significa que a métrica não está disponível naquele aparelho ou build.

**`summary.json`: por bloco**

- **FPS médio:** total de frames ÷ tempo, excluindo o aquecimento.
- **FPS mín./máx. (1 s):** a menor e a maior média de FPS entre as janelas de 1 segundo.
- **1% low / 0,1% low:** FPS médio dos 1% (ou 0,1%) frames mais lentos. Mede travadas.
- **Frame time mediana, p95, p99 e máximo** (ms).
- **CPU/GPU ms médios:** se `gpu_frame_ms` ≈ intervalo de frame, o gargalo é a GPU; se `cpu_main_ms` ou `cpu_render_ms` dominam, é a CPU.
- **Energia:** % inicial e final, **Δ carga (µAh)** via `CHARGE_COUNTER`, corrente média bruta.
- **Térmico:** temperatura da bateria inicial, final e máxima; estado térmico máximo (0 = nenhum … 6 = desligamento); headroom térmico máximo (1,0 = limite de throttling).
- **`wasPlugged`:** indica se houve carregador conectado durante o bloco. **Se `true`, os dados de energia do bloco são inválidos.**
- **`worldHash`:** prova de determinismo (Seção 3).

### 5.2 Por que não usar só a % da bateria

A porcentagem é inteira. Em blocos curtos, a diferença de consumo entre cenários some no arredondamento. O `CHARGE_COUNTER` (µAh) e o `CURRENT_NOW` (µA) têm resolução muito maior.

Cuidados com esses valores:

- O **sinal** e a **unidade** de `CURRENT_NOW` variam por fabricante (alguns Samsung reportam mA ou invertem o sinal). O valor é gravado bruto; interprete por modelo.
- `CHARGE_COUNTER` não existe em todos os aparelhos. Quando falta, use a corrente média × tempo.

### 5.3 Por que a temperatura exige blocos longos

O termistor da bateria e a carcaça do aparelho têm inércia térmica de minutos. Em blocos de 1 minuto, a temperatura reflete principalmente o bloco anterior. Por isso:

- os blocos devem ter **pelo menos 3 minutos**;
- a execução deve começar com o **aparelho em temperatura ambiente** (Seção 6);
- o `thermal_status` e o `thermal_headroom` do Android, quando disponíveis, reagem mais rápido que a temperatura da bateria e mostram quando o sistema começou a reduzir clocks.

## 6. Protocolo de execução

1. **Sem cabo USB durante a medição.** Com o cabo, o aparelho carrega e o consumo fica mascarado. Use adb por Wi-Fi (`adb tcpip 5555` e `adb connect <ip>:5555`, ou a Depuração sem fio). A coluna `plugged` confirma: tem que ser `0`.
2. **Bateria entre 80% e 100%** no início (alguns aparelhos limitam o desempenho com bateria baixa).
3. **Repouso de 15 min** com a tela desligada antes de cada execução, em ambiente a **22–25 °C**. Registre a temperatura ambiente.
4. **Brilho fixo** (ex.: 50%, sem brilho automático), modo avião ou Wi-Fi apenas, sem outros apps abertos.
5. **Modos de economia desligados.** Em Samsung, verifique Game Booster / Game Optimizing Service: esses recursos podem limitar FPS ou resolução de apps identificados como jogo. Registre a configuração usada.
6. **Mesmo APK, mesma seed, mesma sequência** em todos os aparelhos comparados.
7. **3 repetições** por aparelho, com repouso entre elas. Reporte a mediana.

## 7. Calibração dos cenários

**Objetivo:** o **PESADO** deve deixar até um topo de linha **limitado pela GPU** e fazê-lo aquecer e drenar bateria de forma clara. O **LEVE** deve ser a linha de base (todos os aparelhos no teto de FPS, com folga).

### 7.1 Aparelhos de calibração

Use pelo menos um aparelho de cada faixa: **entrada**, **intermediário** e **topo de linha**, de preferência com SoCs de fabricantes diferentes (Snapdragon, Exynos, MediaTek). O cenário é calibrado pelo topo de linha e verificado nos demais.

### 7.2 Passo a passo

1. **Linha de base (LEVE):** rode `seq=light&stage=180`. Todos os aparelhos devem ficar no teto de FPS, com `gpu_frame_ms` bem abaixo do intervalo de frame (ex.: < 5 ms a 120 Hz). Se o aparelho de entrada não atingir o teto, reduza o LEVE.

2. **Isolamento das alavancas:** rode o PESADO desligando um módulo de cada vez com `off=`. Por exemplo:
   ```
   perfbench://start?seq=heavy&stage=120&off=post
   perfbench://start?seq=heavy&stage=120&off=shadows,overdraw
   perfbench://start?seq=heavy&stage=120&off=cpu,physics,vcam
   ```
   Compare `fps_avg`, `gpu_frame_ms` e `cpu_main_ms` para saber quanto cada alavanca pesa em cada aparelho. Ajuste primeiro as alavancas de custo **linear e previsível**: iterações de shader, pixels renderizados e camadas de overdraw.

3. **Ajuste do PESADO no topo de linha:** aumente a carga de GPU até o topo de linha ficar **abaixo do refresh** durante todo o bloco. Uma referência razoável é 30–50% do teto (ex.: 40–60 fps num painel de 120 Hz), com `gpu_frame_ms` ≈ intervalo de frame. Depois, aumente a carga de CPU (elementos do job, corpos físicos) até `cpu_main_ms` ficar em torno de 60–80% do intervalo de frame. Assim os dois ficam ocupados sem a CPU virar o gargalo principal.

4. **Verificação térmica e de energia (topo de linha, 3 blocos PESADO seguidos):**
   - a temperatura da bateria deve **subir de forma contínua**; como referência, ≥ 5 °C em 9 minutos;
   - o `thermal_status` deve chegar a pelo menos **1–2** (leve/moderado), ou o FPS deve cair visivelmente ao longo do bloco (throttling);
   - a corrente média do PESADO deve ser **várias vezes** a do LEVE.

   Se nada disso acontecer, a carga ainda é insuficiente: volte ao passo 3.

5. **MÉDIO:** posicione o MÉDIO entre os dois. O intermediário deve ficar perto do teto e o de entrada já limitado pela GPU.

6. **Verificação nos demais aparelhos:** o aparelho de entrada deve **completar** o PESADO, mesmo com FPS baixo, sem ser encerrado pelo sistema. Confira na memória (`total_mem_mb`) se há folga para aparelhos com 4 GB de RAM.

7. **Congelamento:** quando os parâmetros estiverem definidos, **congele** os três perfis, gere a versão do APK e registre a tabela final de parâmetros junto com a versão. Qualquer mudança posterior invalida a comparação com resultados anteriores.

## 8. Limitações conhecidas

- **Teto por refresh:** aparelhos com painel de 60 Hz e com 120 Hz têm tetos diferentes no LEVE. O resultado principal deve vir dos cenários em que os aparelhos estão limitados por GPU/CPU (MÉDIO e PESADO), e o `maxRefreshHz` de cada aparelho deve ser registrado (vem no `summary.json`).
- **Física em aparelhos muito lentos:** se o frame passar do `Maximum Allowed Timestep`, o Unity descarta passos de física. A quantidade de trabalho de física, então, cai nesse aparelho.
- **Partículas:** a quantidade em tela é equivalente, mas não idêntica bit a bit.
- **Sensores:** as unidades de corrente e a disponibilidade do headroom térmico variam por fabricante e versão do Android.
- **Otimizações do fabricante** (ex.: Game Optimizing Service da Samsung) podem tratar o app como jogo e aplicar limites. Devem ser verificadas e registradas.
