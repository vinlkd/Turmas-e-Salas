// Execute contra uma instância de demonstração isolada, nunca contra dados reais.
import assert from 'node:assert/strict';
const base = process.env.SALAS_TEST_URL || 'http://localhost:5018';
let checks = 0;
function client() {
  const cookies = new Map();
  let csrf = '';
  return {
    async request(path, method = 'GET', body, sendCsrf = true) {
      const headers = { Cookie: [...cookies].map(([k, v]) => `${k}=${v}`).join('; ') };
      if (body) headers['Content-Type'] = 'application/json';
      if (sendCsrf) headers['X-CSRF-TOKEN'] = csrf;
      const response = await fetch(base + path, { method, headers, body: body ? JSON.stringify(body) : undefined });
      for (const cookie of response.headers.getSetCookie()) {
        const [pair] = cookie.split(';');
        const index = pair.indexOf('=');
        cookies.set(pair.slice(0, index), pair.slice(index + 1));
      }
      const text = await response.text();
      const data = text ? JSON.parse(text) : {};
      if (path === '/auth/sessao') csrf = data.csrfToken;
      return { status: response.status, data };
    },
    async login(email, senha) {
      await this.request('/auth/sessao');
      status(await this.request('/auth/login', 'POST', { email, senha }), 200, 'Login válido');
      return (await this.request('/auth/sessao')).data.usuario;
    }
  };
}
function status(result, expected, description) {
  assert.equal(result.status, expected, `${description}: ${JSON.stringify(result.data)}`);
  checks++;
  console.log(`OK: ${description}`);
  return result.data;
}
const anonymous = client(), admin = client(), professor = client();
const config = (await anonymous.request('/auth/sessao')).data;
assert.equal(config.demonstracao, true, 'Este teste só pode usar o modo de demonstração.');
status(await anonymous.request('/reservas'), 401, 'Agenda exige autenticação');
status(await anonymous.request('/auth/login', 'POST', { email: 'admin@senai.demo', senha: 'Admin@123' }, false), 400, 'Login exige token CSRF');
status(await anonymous.request('/auth/login', 'POST', { email: 'admin@senai.demo', senha: 'errada' }), 401, 'Senha incorreta recusada');
const adminUser = await admin.login('admin@senai.demo', 'Admin@123');
const professorUser = await professor.login('professor@senai.demo', 'Professor@123');
assert.equal(adminUser.perfil, 'Admin');
assert.equal(professorUser.perfil, 'Professor');
const roomBody = { nome: `Sala teste ${Date.now()}`, tipo: 'Sala de aula', capacidade: 40, localizacao: 'Bloco de testes', altoDesempenho: false };
status(await professor.request('/salas', 'POST', roomBody), 403, 'Professor não cadastra salas');
status(await professor.request('/turmas', 'POST', { nome: 'Inválida', curso: 'Teste', turno: 'Manhã', quantidadeAlunos: 20 }), 403, 'Professor não cadastra turmas');
status(await admin.request('/salas', 'POST', roomBody, false), 400, 'Cadastro exige CSRF');
const room = status(await admin.request('/salas', 'POST', roomBody), 201, 'Admin cadastra sala');
status(await admin.request('/salas', 'POST', roomBody), 409, 'Nome duplicado de sala é recusado');
const turma = status(await admin.request('/turmas', 'POST', { nome: `Turma teste ${Date.now()}`, curso: 'Mecânica', turno: 'Manhã', quantidadeAlunos: 25 }), 201, 'Admin cadastra turma');
const turma2 = status(await admin.request('/turmas', 'POST', { nome: `Turma concorrente ${Date.now()}`, curso: 'Mecânica', turno: 'Tarde', quantidadeAlunos: 25 }), 201, 'Admin cadastra segunda turma');
const request = { salaId: room.id, turmaId: turma.id, professorId: professorUser.id, atividade: 'Aula prática', nivel: 'Leve', inicio: '2026-11-10T09:00:00-03:00', fim: '2026-11-10T10:00:00-03:00' };
status(await professor.request('/reservas', 'POST', { ...request, professorId: adminUser.id }), 403, 'Professor não assume outro responsável');
status(await professor.request('/reservas', 'POST', { ...request, turmaId: 'nao-existe' }), 404, 'Reserva exige turma cadastrada');
const reserva = status(await professor.request('/reservas', 'POST', request), 201, 'Professor reserva para turma cadastrada');
status(await admin.request('/reservas', 'POST', { ...request, turmaId: turma2.id }), 409, 'Admin precisa liberar horário ocupado');
status(await professor.request(`/reservas/${reserva.id}/cancelar`, 'POST', { motivo: 'Tentativa de cancelamento' }), 403, 'Professor não cancela reservas');
const cancelada = status(await admin.request(`/reservas/${reserva.id}/cancelar`, 'POST', { motivo: 'Reorganização pela coordenação' }), 200, 'Coordenação cancela reserva do professor');
assert.equal(cancelada.status, 'Cancelada');
assert.equal(cancelada.motivoCancelamento, 'Reorganização pela coordenação');
assert.ok(cancelada.canceladoEm);
status(await admin.request(`/reservas/${reserva.id}/cancelar`, 'POST', { motivo: 'Novo cancelamento' }), 409, 'Cancelamento duplicado é recusado');
status(await professor.request('/reservas', 'POST', request), 201, 'Cancelamento libera sala e turma');
const high = { ...request, salaId: 'lab-cad', turmaId: turma2.id };
status(await professor.request('/reservas', 'POST', high), 400, 'Professor respeita regra de alto desempenho');
status(await admin.request('/reservas', 'POST', high), 201, 'Coordenação reserva qualquer tipo de sala');
const concurrency = { ...request, nivel: 'Pesado', inicio: '2026-11-11T09:00:00-03:00', fim: '2026-11-11T10:00:00-03:00' };
const races = await Promise.all([admin.request('/reservas', 'POST', concurrency), professor.request('/reservas', 'POST', { ...concurrency, turmaId: turma2.id })]);
assert.deepEqual(races.map(r => r.status).sort(), [201, 409]); checks++; console.log('OK: Pedidos simultâneos não duplicam horário');
const history = status(await professor.request('/reservas'), 200, 'Professor consulta histórico');
assert.ok(history.some(r => r.id === reserva.id && r.status === 'Cancelada'));
const users = status(await admin.request('/professores'), 200, 'Lista de responsáveis');
assert.ok(users.every(u => !('senhaHash' in u) && !('senha' in u)));
const editedRoomBody = { ...roomBody, nome: `${roomBody.nome} editada`, tipo: 'Laboratório', capacidade: 45, localizacao: 'Bloco novo', altoDesempenho: true };
status(await professor.request(`/salas/${room.id}`, 'PUT', editedRoomBody), 403, 'Professor não edita salas');
status(await admin.request(`/salas/${room.id}`, 'PUT', editedRoomBody, false), 400, 'Edição de sala exige CSRF');
status(await admin.request(`/salas/${room.id}`, 'PUT', { ...editedRoomBody, capacidade: 0 }), 400, 'Capacidade inválida recusada na edição');
status(await admin.request('/salas/nao-existe', 'PUT', editedRoomBody), 404, 'Edição exige sala existente');
status(await admin.request(`/salas/${room.id}`, 'PUT', { ...editedRoomBody, nome: 'Sala 101' }), 409, 'Edição não duplica nome de outra sala');
const updatedRoom = status(await admin.request(`/salas/${room.id}`, 'PUT', editedRoomBody), 200, 'Admin edita todos os campos da sala');
assert.equal(updatedRoom.id, room.id);
for (const [key, value] of Object.entries(editedRoomBody)) assert.equal(updatedRoom[key], value);
status(await admin.request(`/salas/${room.id}`, 'PUT', editedRoomBody), 200, 'Salvar sala com o mesmo nome é permitido');
const editedClassBody = { nome: `${turma.nome} editada`, curso: 'Automação Industrial', turno: 'Noite', quantidadeAlunos: 32 };
status(await professor.request(`/turmas/${turma.id}`, 'PUT', editedClassBody), 403, 'Professor não edita turmas');
status(await admin.request(`/turmas/${turma.id}`, 'PUT', editedClassBody, false), 400, 'Edição de turma exige CSRF');
status(await admin.request(`/turmas/${turma.id}`, 'PUT', { ...editedClassBody, quantidadeAlunos: 0 }), 400, 'Quantidade inválida recusada na edição');
status(await admin.request('/turmas/nao-existe', 'PUT', editedClassBody), 404, 'Edição exige turma existente');
status(await admin.request(`/turmas/${turma.id}`, 'PUT', { ...editedClassBody, nome: turma2.nome }), 409, 'Edição não duplica nome de outra turma');
const updatedClass = status(await admin.request(`/turmas/${turma.id}`, 'PUT', editedClassBody), 200, 'Admin edita todos os campos da turma');
assert.equal(updatedClass.id, turma.id);
for (const [key, value] of Object.entries(editedClassBody)) assert.equal(updatedClass[key], value);
const savedRooms = status(await professor.request('/salas'), 200, 'Sala editada disponível para consulta');
assert.equal(savedRooms.find(s => s.id === room.id).nome, editedRoomBody.nome);
const savedClasses = status(await professor.request('/turmas'), 200, 'Turma editada disponível para consulta');
assert.equal(savedClasses.find(t => t.id === turma.id).curso, editedClassBody.curso);
const afterEdit = status(await admin.request('/reservas'), 200, 'Edição preserva vínculo das reservas');
assert.ok(afterEdit.some(r => r.id === reserva.id && r.salaId === room.id && r.turmaId === turma.id));
const startsSoon = new Date(Date.now() + 20 * 60 * 1000);
const soonRequest = { salaId: room.id, turmaId: turma2.id, atividade: 'Reserva próxima do limite', nivel: 'Leve',
  inicio: startsSoon.toISOString(), fim: new Date(startsSoon.getTime() + 60 * 60 * 1000).toISOString() };
const soonReservation = status(await admin.request('/reservas', 'POST', soonRequest), 201, 'Reserva próxima do início é criada');
status(await admin.request(`/reservas/${soonReservation.id}/cancelar`, 'POST', { motivo: 'Teste do limite de cancelamento' }), 409, 'Cancelamento é bloqueado com menos de 30 minutos');
const startsLater = new Date(Date.now() + 100 * 60 * 1000);
const laterRequest = { ...soonRequest, turmaId: turma.id, atividade: 'Reserva dentro do prazo',
  inicio: startsLater.toISOString(), fim: new Date(startsLater.getTime() + 60 * 60 * 1000).toISOString() };
const laterReservation = status(await admin.request('/reservas', 'POST', laterRequest), 201, 'Reserva com mais de 30 minutos é criada');
status(await admin.request(`/reservas/${laterReservation.id}/cancelar`, 'POST', { motivo: 'Teste do prazo permitido' }), 200, 'Cancelamento é permitido com mais de 30 minutos');
status(await admin.request('/auth/logout', 'POST'), 204, 'Logout encerra sessão');
status(await admin.request('/salas'), 401, 'Sessão encerrada perde acesso');
console.log(`${checks} verificações da API passaram.`);
