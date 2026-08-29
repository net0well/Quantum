# Contribuindo com o Quantum

## Ambiente

- Windows 10 (1809+) ou Windows 11, 64 bits
- [.NET SDK 10](https://dotnet.microsoft.com/download) — a versão exata está no `global.json`

```powershell
git clone https://github.com/net0well/Quantum.git
cd Quantum
dotnet build Quantum.slnx
dotnet run --project src\Quantum.App\Quantum.App.csproj
```

## Antes de abrir um PR

```powershell
dotnet build Quantum.slnx -c Release   # avisos são erros: precisa sair limpo
dotnet test
```

O `Directory.Build.props` liga `TreatWarningsAsErrors`. Isso é proposital: um aviso
do compilador em código de interop costuma ser um bug esperando para acontecer.

## Como o projeto está organizado

`src/Quantum.Audio` é uma biblioteca sem nenhuma dependência de interface — dá para
usar em linha de comando ou em um serviço. `src/Quantum.App` é só a camada WPF.
Se a sua mudança é de comportamento de áudio, ela pertence à biblioteca, com teste.

## Mexendo em interop COM

É a parte mais delicada do projeto. Duas regras que vieram de bugs reais:

1. **Confira o tamanho das structs.** `PROPVARIANT` tem 24 bytes em x64. Declarar
   menos faz a chamada COM escrever além da struct e derrubar o processo com um
   `Internal CLR error` sem stack útil. Há testes travando esses tamanhos — mantenha-os.

2. **Seja explícito no empacotamento de arrays.** Em interfaces COM o padrão é
   `SAFEARRAY`. APIs do Core Audio esperam ponteiro simples, então use
   `[MarshalAs(UnmanagedType.LPArray)]`.

Mudança em interop **precisa ser testada contra hardware real**, não só compilada.
Compilar não prova nada aqui: os dois bugs acima compilavam perfeitamente.

## Estilo

O `.editorconfig` manda. Em resumo: `namespace` com escopo de arquivo, `var` quando
o tipo é óbvio, chaves sempre, campos privados com `_`.

Comentário explica **por que**, não o que o código faz. Se o código precisa de
comentário para dizer o que faz, reescreva o código.

## Padrões de projeto: o nome tem que entregar o padrão

Se uma classe implementa um padrão, **o sufixo diz qual**. Quem abre o arquivo
descobre a intenção pelo nome, sem ler a implementação:

| Padrão | Sufixo | Exemplo no projeto |
|---|---|---|
| Strategy | `...Strategy` | `ChannelImbalanceHealthCheckStrategy`, `QualityProfileStepStrategy` |
| Factory | `...Factory` | `DeviceViewModelFactory` |
| Repository | `...Repository` | `JsonProfileRepository` |
| Adapter | `...Adapter` | `TrayIconAdapter` |

Vale o contrário também: **não use o sufixo sem o padrão**. Uma classe chamada
`XFactory` que não fabrica nada é pior que uma sem nome nenhum.

E o sufixo não é licença para espalhar padrão onde não precisa. O critério é
sempre a dor concreta: as verificações de saúde viraram Strategy porque cada
uma nova exigiria editar o `HealthMonitor`; o `TrayIconAdapter` tem esse nome
porque de fato adapta a API do WinForms ao modelo de eventos do WPF.

Ao mexer numa área, vale olhar se algum padrão já aplicado ali cabe no que você
está acrescentando — o objetivo é o projeto inteiro conversar da mesma forma.

## Mensagens de commit

Descreva o efeito, não o arquivo alterado:

```
Corrige medidor derrubando o app em dispositivos com 6 canais
Adiciona perfil para chamadas
```

Para entrar na `main` sem gerar release, inclua `[skip release]` na mensagem.

## Releases

São automáticas: todo commit que entra na `main` roda build, testes e publicação, e
cria uma release nova com o executável para download. Para mudar `major.minor`,
edite `<Version>` no `Directory.Build.props` — o patch vem do número da execução.
