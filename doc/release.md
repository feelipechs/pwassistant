# Release — como publicar o PwAssistant

Guia do mantenedor. Versão vem da tag (`vX.Y.Z`); `packId PwAssistant`
é estável e definitivo (mudar quebra updates de quem já instalou).

## 1. Publicar o app

```powershell
dotnet publish src/PwAssistant.App -c Release -r win-x64 --self-contained /p:PublishSingleFile=true -o ./publish
Remove-Item "publish\*.pdb"
dnx vpk@1.0.1 pack --packId PwAssistant --packVersion X.Y.Z --packDir ./publish --mainExe PwAssistant.App.exe
```

O `vpk` verifica `VelopackApp.Run()` no `Main()` customizado, gera o
delta contra a versão anterior presente em `Releases/` e cria o
`Setup.exe`. Sem certificado: bundle sem assinatura (SmartScreen
esperado — avisar no texto do release).

## 2. Criar o Release no GitHub

Tag `vX.Y.Z` sobre a `main`, título `vX.Y.Z`, anexos de `Releases/`:

- `PwAssistant-win-Setup.exe` (o que os usuários baixam);
- `PwAssistant-X.Y.Z-full.nupkg` (+ `-delta.nupkg` quando gerado);
- `releases.win.json` (**sempre o atualizado do último pack** — é o
  feed que o updater lê; sem ele não há update).

## 3. Testar o update fim-a-fim

Com a versão anterior instalada: abrir o app → prompt
"VERSÃO X.Y.Z DISPONÍVEL" → confirmar → aplica o delta e reinicia
sozinho → versão nova no canto da janela principal. Na abertura
seguinte, sem prompt.
