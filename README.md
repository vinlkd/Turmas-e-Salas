# Gestão de Salas e Turmas — SENAI

Protótipo em ASP.NET Core 8 com login, agenda e cadastros. Atende salas de aula, informática, laboratórios, oficinas, auditórios e outros espaços.

## Iniciar

Na pasta do projeto:

```powershell
dotnet run --project Salas.csproj --launch-profile http
```

Abra http://localhost:5008. O Swagger está em http://localhost:5008/swagger. Use Ctrl+C no terminal para encerrar antes de compilar novamente. Uma instância aberta pode bloquear o executável no Windows.

## Contas de demonstração

O perfil Development usa `Demo:Enabled=true`. Os botões na tela de login preenchem as credenciais:

| Perfil | E-mail | Senha |
| --- | --- | --- |
| Coordenação | admin@senai.demo | Admin@123 |
| Professor | professor@senai.demo | Professor@123 |

Os dados locais ficam em `App_Data/demo.json`, fora dos arquivos públicos e ignorados pelo Git. Os cadastros e as reservas persistem ao reiniciar. As senhas são armazenadas com hash BCrypt. O modo local usa um bloqueio para serializar operações e é destinado a uma única instância do protótipo. Ele só é habilitado em Development.

## Perfis e funcionalidades

O menu **Conta | Sair** permite editar os próprios dados ou encerrar a sessão. Professores e coordenação podem atualizar nome, e-mail e senha. A senha atual é obrigatória para alterar o e-mail ou a senha; mudar apenas o nome não exige nova confirmação. O perfil de acesso e o ID da conta são preservados. O novo e-mail e a nova senha passam a valer no próximo login.

- **Professor:** consulta a agenda e as turmas, faz reservas em seu próprio nome para turmas cadastradas. Não cadastra salas ou turmas e não cancela reservas.
- **Coordenação / Admin:** cadastra e edita salas e turmas, reserva qualquer tipo de sala para qualquer turma cadastrada e escolhe o responsável. Pode cancelar reservas, inclusive as feitas pelos professores.
- Use **Editar sala** ou **Editar turma** nos cartões para carregar os dados no formulário. **Salvar alterações** atualiza o cadastro; **Cancelar edição** descarta o preenchimento. Os IDs são preservados, mantendo os vínculos com as reservas.
- O cancelamento libera o horário da sala e da turma. O registro continua no histórico com motivo, autor e data. Ative “Incluir reservas canceladas” para consultá-lo.
- A coordenação só pode cancelar uma reserva até 30 minutos antes do início. Quando esse limite passa, a ação é bloqueada também pela API, mesmo que alguém tente chamar o endpoint diretamente.
- Durante o intervalo entre o início e o fim, a agenda identifica a reserva como **Reserva em andamento**. O status é calculado no momento da consulta e volta a ser exibido normalmente depois do término.
- Uma turma não pode ter duas reservas simultâneas. Um conflito de sala não é ignorado pelo administrador: ele pode cancelar a reserva anterior e fazer outra.
- Professores continuam sujeitos à regra de uso intensivo para salas marcadas como alto desempenho. A coordenação pode usar essas salas também em atividades comuns.
- A prioridade existente para uso intensivo permite remanejar reservas comuns apenas para espaços do mesmo tipo, disponíveis durante todo o período e com capacidade igual ou superior à sala original. Se não houver alternativa, retorna conflito sem alterar os dados.
- As datas da interface usam o fuso do navegador, identificado abaixo da agenda; a API recebe e retorna UTC. Horários consecutivos não conflitam.

## API e autenticação

| Método | Rota | Acesso |
| --- | --- | --- |
| GET | /auth/sessao | Público; retorna sessão e token CSRF |
| POST | /auth/login | Público; e-mail e senha |
| POST | /auth/logout | Autenticado |
| GET / PUT | /auth/conta | Autenticado; consulta ou edita somente a própria conta |
| GET | /salas, /turmas, /professores, /reservas | Autenticado |
| POST | /salas, /turmas | Admin |
| PUT | /salas/{id}, /turmas/{id} | Admin; mesmos campos do cadastro |
| POST | /reservas | Admin ou Professor |
| POST | /reservas/{id}/cancelar | Admin; corpo com motivo |

A autenticação usa cookie HttpOnly. Todas as operações POST e PUT exigem o cabeçalho `X-CSRF-TOKEN`, obtido em `/auth/sessao`, mantendo os cookies. Depois do login, consulte a sessão novamente para obter o token vinculado ao usuário. Login tem limite de tentativas; o perfil vem do cadastro no servidor. Ocultar botões no frontend não substitui os controles na API.

O cadastro de sala recebe `nome`, `tipo`, `capacidade`, `localizacao` e `altoDesempenho`. O de turma recebe `nome`, `curso`, `turno` e `quantidadeAlunos`. A reserva recebe:

```json
{
  "salaId": "sala-101",
  "turmaId": "dev-2026",
  "atividade": "Aula de programação",
  "nivel": "Leve",
  "inicio": "2026-10-09T09:00:00-03:00",
  "fim": "2026-10-09T11:00:00-03:00"
}
```

`professorId` é opcional: por padrão usa o usuário autenticado. Só a coordenação pode informar outro responsável. As respostas nunca retornam hashes de senha.

## Usar Firestore

Desative a demonstração e configure as credenciais do seu projeto. O modo Firestore não recebe as contas de demonstração automaticamente:

```powershell
$env:Demo__Enabled = "false"
$env:Firestore__ProjectId = "seu-projeto-firebase"
$env:Firestore__CredentialsPath = "C:\credenciais\firebase-key.json"
$env:Bootstrap__Admin__Email = "coordenacao@exemplo.com"
$env:Bootstrap__Admin__Nome = "Coordenação"
$env:Bootstrap__Admin__Senha = "SUBSTITUA_POR_UMA_SENHA_FORTE"
dotnet run --project Salas.csproj --launch-profile http
```

As variáveis `Bootstrap__Professor__Email`, `Bootstrap__Professor__Nome` e `Bootstrap__Professor__Senha` provisionam uma conta de professor da mesma forma. A senha inicial deve ter pelo menos 10 caracteres. O provisionamento cria somente contas cujo e-mail ainda não existe; não altera contas existentes. Depois de criar as contas, remova as variáveis Bootstrap do ambiente. Não há tela de cadastro público de usuários nesta versão.

As coleções são `usuarios`, `salas`, `turmas` e `reservas`. Campos de modelos no Firestore usam nomes em PascalCase. O documento `controle/escola` serializa alterações dentro de transações; todas as operações da aplicação devem usar esse mesmo protocolo. A versão de protótipo lê as coleções completas e grava somente documentos alterados. Para uma base maior, será necessário limitar as consultas.

Reservas antigas sem `TurmaId` continuam visíveis. Novas reservas exigem turma cadastrada. Se houver dados da versão inicial, ajuste `AltoAtendimento` para `AltoDesempenho`, informe o `Tipo` das salas e cadastre os usuários com login; a coleção antiga `professores` não é usada na autenticação.

Referências: [cookies no ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie), [proteção CSRF](https://learn.microsoft.com/aspnet/core/security/anti-request-forgery) e [transações Firestore](https://docs.cloud.google.com/dotnet/docs/reference/Google.Cloud.Firestore/latest/Google.Cloud.Firestore.Transaction).

## Verificações

Com a aplicação encerrada:

```powershell
dotnet build Salas.csproj
dotnet run --project Tests/Salas.Checks.csproj
```

Os testes de API e navegador usam uma instância isolada, com dados próprios:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project Salas.csproj --no-build --no-launch-profile -- --urls http://localhost:5018 --Demo:DataPath App_Data/testes.json
```

Em outro terminal, execute `node Tests/api-checks.mjs`. O teste de navegador é `node Tests/browser-checks.cjs` e exige Playwright e Microsoft Edge; `SALAS_PLAYWRIGHT_PATH` pode indicar o módulo Playwright instalado. Encerre a instância de testes com Ctrl+C ao terminar para liberar a compilação.

Foram verificados os dois logins, permissões, CSRF, cadastros, conflitos simultâneos, cancelamento com histórico, liberação de horários e layout móvel no modo local. A integração com Firestore requer as credenciais e deve ser validada separadamente.
