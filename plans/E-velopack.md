# Fase E — instalador + updater (Velopack + GitHub Releases)

## Decisão

**Velopack** (open source, MIT): um `vpk pack` gera `Setup.exe` **e**
updates delta, sem UAC no update, com sample oficial WPF e publicação
direta em GitHub Releases. Inno cobriria só o instalador (updater
seria caseiro); MSIX exige certificado e trava o modelo atual.

## Pré-requisitos

- Fases A–D verdes (distribuir só sobre base segura).
- Repositório GitHub com permissão de criar Releases.
- Publish atual: single-file self-contained win-x64 (~147 MB) em
  `./publish` — o `vpk` empacota o diretório de saída como está
  (inclui `Resources/`).

## Passos

1. NuGet `Velopack` no `PwAssistant.App`; ~10 linhas no `App.xaml.cs`:
   `VelopackApp.Build().Run()` (hooks de install/update) +
   `UpdateManager` checando o feed no startup em background, com
   prompt em pt-BR ("Atualização disponível, aplicar e reiniciar?").
   Update aplica + reinicia sozinho.
2. Versionamento por tag git (`v1.0.0` → versão do pacote); anotar o
   procedimento no plano durante a execução.
3. `vpk pack` local → instalar `Setup.exe` numa máquina/VM limpa →
   abrir pelo atalho instalado → publicar release de teste no GitHub
   → subir versão nova → confirmar download delta + aplicação +
   restart fim-a-fim.
4. Texto de orientação no release sobre SmartScreen/antivírus
   (app sem certificado de code signing — pago — será sinalizado
   até ganhar reputação; sem alternativa grátis).

## Aceite

- [ ] `Setup.exe` instala e o app abre pelo atalho em máquina limpa.
- [ ] Versão nova no feed → prompt pt-BR → aplica delta → reinicia
      sozinho na versão nova.
- [ ] `dotnet build` 0/0 + `dotnet test` verde.

## Procedimento testado (2026-09-25, nesta máquina)

Integração no código (já aplicada):

- NuGet `Velopack 1.0.1` no `PwAssistant.App`; `VelopackApp.Build().Run()`
  como **primeira linha do `Main()` customizado** (`App.xaml` virou
  `Page` + `StartupObject PwAssistant.App.App` no csproj — o `vpk`
  exige e verifica: `Verified VelopackApp.Run() in '...App::Main'`).
- `Services/AppUpdater.cs`: `GithubSource("https://github.com/feelipechs/pwassistant", null, false)`
  (repositório público, sem token, só stable); pula quando
  `!IsInstalled` (builds de dev); prompt via `IDialogService`
  (`UpdateAvailableTitle/Confirm/Apply` em EN+PT); download → 
  `ApplyUpdatesAndRestart`. Tudo observado em log, nunca quebra o startup.
- `Releases/` e `publish/` no `.gitignore`.

Release (na hora de publicar, tag `vX.Y.Z`):

```powershell
dotnet publish src/PwAssistant.App -c Release -r win-x64 --self-contained /p:PublishSingleFile=true -o ./publish
Remove-Item "publish\*.pdb"
dnx vpk@1.0.1 pack --packId PwAssistant --packVersion X.Y.Z --packDir ./publish --mainExe PwAssistant.App.exe
# subir Releases/PwAssistant-*-full.nupkg + releases.win.json
# como assets do GitHub Release vX.Y.Z (vpk upload github faz isso)
```

Notas:

- `packId PwAssistant` é estável e definitivo (mudar quebra updates;
  o `Ditto.PwAssistant` da 1.0.0 local nunca foi distribuído).
- Sem certificado: `Setup.exe` sem assinatura → SmartScreen até ganhar
  reputação. Avisar no texto do release.
- `vpk` avisa que existe CLI 1.2.x; lib fixada em 1.0.1 (testada).
  Só mexer com pedido explícito.
