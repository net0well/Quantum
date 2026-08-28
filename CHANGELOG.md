# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/).
As versões seguem `major.minor.patch`, onde o patch é o número da execução do
workflow de release — toda entrada na `main` publica uma versão nova.

## [Não lançado]

## [1.0.5]

### Manutenção

- xunit.v3 de 3.2.2 para 4.0.0
- `actions/checkout` para v7, `actions/setup-dotnet` e `actions/cache` para v6,
  encerrando o aviso de depreciação do Node.js 20 nos workflows

## [1.0.1]

Primeira versão publicada. O patch das versões é o número da execução do workflow,
por isso a primeira release é `1.0.1` e não `1.0.0`.

### Adicionado

- **Balanço e canais** — controle de −100 a +100, nível por canal em porcentagem e
  em dB reais, botão para igualar os canais e leitura do desvio entre eles.
- **Medidores ao vivo** — pico por canal em barras segmentadas, lidos do
  `IAudioMeterInformation`.
- **Microfone** — dispositivos de entrada com a mesma régua de controle da saída,
  mais um medidor de nível dedicado.
- **Qualidade** — lista apenas os formatos que o hardware aceita de fato, sondados
  em modo exclusivo, e permite trocar o formato padrão do endpoint.
- **Áudio espacial** — leitura e troca entre Windows Sonic, Dolby Atmos e DTS:X,
  sinalizando quais exigem um app da Microsoft Store.
- **Driver** — descrição, fornecedor, versão, data, serviço, INF e instância PnP,
  com atalhos para o Gerenciador de Dispositivos e os painéis de som do Windows.
- **Perfis** — FPS competitivo, Filmes e séries, Música e Chamadas, cada um com a
  justificativa das escolhas; perfis próprios podem ser salvos a partir do estado atual.
- **Verificação do sistema** — encontra canais desbalanceados, dispositivo padrão no
  mudo, volume muito baixo, áudio mono ligado, ducking ativo e espacialização ligada
  em fone, com correção em um clique.
- **Segundo plano** — ícone na bandeja, início junto com o Windows, verificação
  periódica configurável e aviso quando algo sai do lugar.
- **Publicação portátil** — executável único e autocontido, sem instalador e sem
  exigir .NET na máquina de destino.

### Notas técnicas

- `PROPVARIANT` declarado com o tamanho correto (24 bytes em x64); tamanho menor
  corrompia a pilha na chamada COM.
- `GetChannelsPeakValues` marcado como `LPArray`; o empacotamento padrão como
  `SAFEARRAY` derrubava o processo na primeira leitura de pico.
- Sondagem de formatos feita em modo exclusivo, porque em modo compartilhado o motor
  de áudio aceita quase tudo e converte por baixo dos panos.
- Layout do catálogo de áudio espacial levantado por comparação entre todos os
  endpoints da máquina, já que o Windows não publica API para isso.
