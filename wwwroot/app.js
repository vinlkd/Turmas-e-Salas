'use strict';

const $ = id => document.getElementById(id);
const state = { salas: [], professores: [], turmas: [], reservas: [], usuario: null, csrf: '', ready: false, saving: false };
const isAdmin = () => state.usuario?.perfil === 'Admin';
const today = () => {
  const date = new Date();
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
};
const time = value => new Date(value).toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit' });
const localDate = value => {
  const date = new Date(value);
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
};
const element = (tag, className, text) => {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text !== undefined) node.textContent = text;
  return node;
};
const roomName = id => state.salas.find(s => s.id === id)?.nome || id;
const teacherName = id => state.professores.find(p => p.id === id)?.nome || id;
const className = id => state.turmas.find(t => t.id === id)?.nome || 'Turma não informada';
const normalize = text => text.normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();

async function api(path, options = {}) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 15000);
  try {
    const response = await fetch(path, { ...options, headers: { ...options.headers, 'X-CSRF-TOKEN': state.csrf }, signal: controller.signal });
    const text = await response.text();
    let body = {};
    try { body = text ? JSON.parse(text) : {}; } catch { /* Resposta sem JSON. */ }
    if (!response.ok) {
      if (response.status === 401 && path !== '/auth/login') {
        showLogin();
        throw new Error('Sua sessão expirou. Entre novamente.');
      }
      if (response.status === 403) throw new Error('Esta ação está disponível apenas para a coordenação.');
      if (response.status === 429) throw new Error('Muitas tentativas. Aguarde um minuto antes de tentar novamente.');
      if (response.status >= 500) throw new Error('O serviço de reservas está indisponível. Tente novamente em instantes.');
      throw new Error(body.errors ? Object.values(body.errors).flat().join(' ') : body.detail || 'Não foi possível concluir a solicitação.');
    }
    return body;
  } catch (error) {
    if (error.name === 'AbortError' || error instanceof TypeError) {
      throw new Error(options.method === 'POST'
        ? 'Não foi possível confirmar a resposta. Atualize a agenda antes de tentar novamente, pois a reserva pode ter sido registrada.'
        : 'Não foi possível conectar ao serviço. Tente atualizar a agenda.');
    }
    throw error;
  } finally { clearTimeout(timeout); }
}

function fillSelect(select, items, label, placeholder) {
  const previous = select.value;
  select.replaceChildren(new Option(placeholder, ''));
  items.forEach(item => select.add(new Option(label(item), item.id)));
  if (items.some(item => item.id === previous)) select.value = previous;
}

function updateRooms() {
  const heavy = document.querySelector('input[name="nivel"]:checked').value === 'Pesado';
  const rooms = state.salas.filter(room => isAdmin() || heavy || !room.altoDesempenho);
  fillSelect($('room'), rooms, room => `${room.nome} · ${room.capacidade} lugares`,
    rooms.length ? 'Selecione uma sala' : 'Nenhuma sala compatível cadastrada');
}

function empty(title, description, action = false) {
  const container = element('div', 'empty');
  const icon = element('span', 'empty-icon', '▦');
  icon.setAttribute('aria-hidden', 'true');
  container.append(icon, element('h3', '', title), element('p', '', description));
  if (action) {
    const link = element('a', 'button subtle', '＋ Reservar um espaço');
    link.href = '#nova-reserva';
    container.append(link);
  }
  return container;
}

function renderAgenda() {
  const selected = $('agenda-date').value;
  if (!selected) return;
  $('day-label').textContent = new Date(`${selected}T12:00:00`).toLocaleDateString('pt-BR', { weekday: 'long', day: 'numeric', month: 'long' });
  if (!state.ready) return;
  // Inclui reservas que atravessam a meia-noite, sem contar o dia em que terminam às 00h.
  const dayStart = new Date(`${selected}T00:00:00`);
  const dayEnd = new Date(dayStart);
  dayEnd.setDate(dayEnd.getDate() + 1);
  const daily = state.reservas.filter(r => new Date(r.inicio) < dayEnd && new Date(r.fim) > dayStart);
  const query = normalize($('search').value.trim());
  const filtered = daily.filter(r => ($('show-cancelled').checked || r.status !== 'Cancelada')
    && normalize(`${r.atividade} ${roomName(r.salaId)} ${teacherName(r.professorId)} ${className(r.turmaId)}`).includes(query))
    .sort((a, b) => new Date(a.inicio) - new Date(b.inicio));
  $('total-dia').textContent = daily.filter(r => r.status !== 'Cancelada').length;
  $('reservation-count').textContent = `${filtered.length} ${filtered.length === 1 ? 'reserva' : 'reservas'}`;
  const list = $('reservations');
  list.replaceChildren();
  if (!filtered.length) {
    list.append(empty(query ? 'Nenhuma reserva encontrada' : 'Sua próxima aula começa aqui.',
      query ? 'Tente outro nome de atividade, sala ou professor.' : 'Não há reservas para este dia. Escolha um espaço e planeje sua atividade.', !query));
    return;
  }
  filtered.forEach(reserva => {
    const moved = reserva.status === 'Remanejada';
    const cancelled = reserva.status === 'Cancelada';
    const row = element('article', 'reservation');
    const hours = element('div', 'reservation-time', time(reserva.inicio));
    hours.append(element('small', '', `até ${time(reserva.fim)}`));
    if (localDate(reserva.inicio) !== selected || localDate(reserva.fim) !== selected) {
      hours.append(element('small', '', `${new Date(reserva.inicio).toLocaleDateString('pt-BR')} a ${new Date(reserva.fim).toLocaleDateString('pt-BR')}`));
    }
    const body = element('div', `reservation-body${cancelled ? ' cancelled' : moved ? ' moved' : ''}`);
    const head = element('div', 'reservation-head');
    head.append(element('h3', '', reserva.atividade), element('span', `badge${cancelled ? ' cancelled' : moved ? ' moved' : ''}`, reserva.status));
    body.append(head, element('p', '', `${roomName(reserva.salaId)} · ${className(reserva.turmaId)}`), element('p', '', teacherName(reserva.professorId)), element('span', 'usage-tag', reserva.nivel === 'Pesado' ? 'Uso intensivo' : 'Uso comum'));
    if (cancelled) body.append(element('p', 'cancel-reason', `Cancelada pela coordenação: ${reserva.motivoCancelamento}`));
    if (isAdmin() && !cancelled) {
      const cancel = element('button', 'cancel-button', 'Cancelar reserva');
      cancel.type = 'button';
      cancel.addEventListener('click', () => openCancel(reserva));
      body.append(cancel);
    }
    row.append(hours, body);
    list.append(row);
  });
}

function renderRooms() {
  $('rooms-list').replaceChildren();
  if (!state.salas.length) $('rooms-list').append(element('p', 'muted', 'Nenhuma sala cadastrada.'));
  state.salas.forEach(room => {
    const card = element('article', 'room-card');
    card.append(element('span', 'room-symbol', room.altoDesempenho ? '↗' : '▤'), element('h3', '', room.nome),
      element('p', '', `${room.tipo} · ${room.capacidade} lugares`), element('p', '', room.localizacao),
      element('span', 'room-type', room.altoDesempenho ? 'Computadores de alto desempenho' : 'Atividades gerais'));
    if (isAdmin()) {
      const edit = element('button', 'button subtle edit-button', 'Editar sala');
      edit.type = 'button';
      edit.dataset.editRoom = room.id;
      edit.setAttribute('aria-label', `Editar sala ${room.nome}`);
      edit.addEventListener('click', () => openEdit('room-form', room));
      card.append(edit);
    }
    $('rooms-list').append(card);
  });
}

function renderClasses() {
  $('classes-list').replaceChildren();
  if (!state.turmas.length) $('classes-list').append(element('p', 'muted', 'Nenhuma turma cadastrada.'));
  state.turmas.forEach(turma => {
    const card = element('article', 'room-card');
    card.append(element('span', 'room-symbol', '▥'), element('h3', '', turma.nome), element('p', '', turma.curso),
      element('span', 'room-type', `${turma.turno} · ${turma.quantidadeAlunos} alunos`));
    if (isAdmin()) {
      const edit = element('button', 'button subtle edit-button', 'Editar turma');
      edit.type = 'button';
      edit.dataset.editClass = turma.id;
      edit.setAttribute('aria-label', `Editar turma ${turma.nome}`);
      edit.addEventListener('click', () => openEdit('class-form', turma));
      card.append(edit);
    }
    $('classes-list').append(card);
  });
}

async function load() {
  const usuarioId = state.usuario?.id;
  if (!usuarioId) return false;
  $('refresh').disabled = true;
  $('connection').textContent = 'Atualizando…';
  try {
    const [salas, professores, turmas, reservas] = await Promise.all([api('/salas'), api('/professores'), api('/turmas'), api('/reservas')]);
    if (state.usuario?.id !== usuarioId) return false;
    Object.assign(state, { salas, professores, turmas, reservas, ready: true });
    $('connection').textContent = 'Agenda conectada';
    $('connection').className = 'connection online';
    $('notice').hidden = true;
    $('total-salas').textContent = salas.length;
    $('total-potentes').textContent = salas.filter(s => s.altoDesempenho).length;
    fillSelect($('professor'), isAdmin() ? professores : professores.filter(p => p.id === usuarioId), p => p.nome,
      'Selecione o responsável');
    if (!$('professor').value) $('professor').value = usuarioId;
    fillSelect($('class'), turmas, t => `${t.nome} · ${t.turno}`, turmas.length ? 'Selecione uma turma' : 'Nenhuma turma cadastrada');
    updateRooms();
    renderAgenda();
    renderRooms();
    renderClasses();
    if (!salas.length || !turmas.length) {
      $('notice').textContent = isAdmin() ? 'Cadastre salas e turmas para começar a fazer reservas.' : 'A coordenação precisa cadastrar salas e turmas para liberar as reservas.';
      $('notice').hidden = false;
    }
    return true;
  } catch (error) {
    $('connection').textContent = 'Sem conexão';
    $('connection').className = 'connection offline';
    $('notice').textContent = error.message + (state.ready ? ' A agenda exibe a última atualização disponível.' : ' As reservas aparecerão assim que a conexão for restabelecida.');
    $('notice').hidden = false;
    if (!state.ready) {
      $('reservations').replaceChildren(empty('Agenda indisponível', 'Use o botão de atualizar para tentar conectar novamente.'));
      $('rooms-list').replaceChildren(element('p', 'muted', 'Não foi possível carregar as salas.'));
      $('professor').replaceChildren(new Option('Aguardando conexão', ''));
    }
    return false;
  } finally {
    $('refresh').disabled = false;
    $('booking-fields').disabled = state.saving || !state.ready || !state.salas.length || !state.turmas.length;
  }
}

function formMessage(message, success = false) {
  $('form-message').textContent = message;
  $('form-message').className = `form-message${success ? ' success' : ''}`;
  $('form-message').hidden = false;
}

$('booking-form').addEventListener('submit', async event => {
  event.preventDefault();
  if (state.saving) return;
  const form = new FormData(event.currentTarget);
  const start = new Date(`${form.get('data')}T${form.get('inicio')}:00`);
  const end = new Date(`${form.get('data')}T${form.get('fim')}:00`);
  if (!Number.isFinite(start.getTime()) || !Number.isFinite(end.getTime()) || end <= start) {
    formMessage('O término deve ser posterior ao início da atividade.');
    $('end').focus();
    return;
  }
  const payload = { salaId: form.get('salaId'), professorId: form.get('professorId'), turmaId: form.get('turmaId'), atividade: String(form.get('atividade')).trim(), nivel: form.get('nivel'), inicio: start.toISOString(), fim: end.toISOString() };
  if (!payload.atividade) { formMessage('Informe a atividade da reserva.'); $('activity').focus(); return; }
  state.saving = true;
  $('booking-fields').disabled = true;
  $('submit').textContent = 'Confirmando…';
  $('form-message').hidden = true;
  try {
    await api('/reservas', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
    $('agenda-date').value = String(form.get('data'));
    $('search').value = '';
    $('activity').value = '';
    const refreshed = await load();
    formMessage(refreshed ? 'Reserva confirmada! Sua atividade já está na agenda.' : 'Reserva confirmada! Atualize a agenda para visualizar os novos dados.', true);
  } catch (error) { formMessage(error.message); }
  finally {
    state.saving = false;
    $('booking-fields').disabled = !state.ready || !state.salas.length || !state.turmas.length;
    $('submit').textContent = 'Confirmar reserva →';
  }
});

$('agenda-date').value = today();
$('booking-date').value = today();
$('timezone').textContent = `Horários: ${Intl.DateTimeFormat().resolvedOptions().timeZone}`;
$('agenda-date').addEventListener('change', renderAgenda);
$('search').addEventListener('input', renderAgenda);
$('show-cancelled').addEventListener('change', renderAgenda);
$('today').addEventListener('click', () => { $('agenda-date').value = today(); renderAgenda(); });
$('refresh').addEventListener('click', load);
document.querySelectorAll('input[name="nivel"]').forEach(radio => radio.addEventListener('change', updateRooms));
document.querySelectorAll('.nav-item').forEach(link => link.addEventListener('click', () => {
  document.querySelectorAll('.nav-item').forEach(item => item.classList.toggle('active', item === link));
}));
renderAgenda();

function showLogin() {
  Object.assign(state, { usuario: null, ready: false, salas: [], professores: [], turmas: [], reservas: [] });
  $('app-shell').hidden = true;
  $('login-screen').hidden = false;
  $('booking-fields').disabled = true;
  if ($('cancel-dialog').open) $('cancel-dialog').close();
  ['room-form', 'class-form'].forEach(id => {
    resetEdit(id);
    $(id === 'room-form' ? 'room-message' : 'class-message').hidden = true;
  });
}

async function session() {
  const data = await api('/auth/sessao');
  state.csrf = data.csrfToken;
  state.usuario = data.usuario;
  $('demo-accounts').hidden = !data.demonstracao;
  if (!data.usuario) { showLogin(); return; }
  $('login-screen').hidden = true;
  $('app-shell').hidden = false;
  $('user-name').textContent = data.usuario.nome;
  $('profile-label').textContent = isAdmin() ? 'ESPAÇO DA COORDENAÇÃO' : 'ESPAÇO DO PROFESSOR';
  document.querySelectorAll('[data-admin]').forEach(node => { node.hidden = !isAdmin(); });
  $('banner-title').textContent = isAdmin() ? 'Sua visão completa dos espaços.' : 'O espaço certo para cada turma.';
  $('banner-description').textContent = isAdmin()
    ? 'Cadastre e edite salas e turmas, reserve qualquer espaço e cancele reservas para reorganizar a agenda da unidade.'
    : 'Reserve espaços para suas turmas. A coordenação pode cancelar reservas quando precisar reorganizar a agenda.';
  $('booking-hint').textContent = isAdmin()
    ? 'Você pode reservar qualquer sala para qualquer turma cadastrada. Em caso de conflito, cancele a reserva anterior na agenda.'
    : 'A disponibilidade será verificada ao confirmar. Salas de alto desempenho exigem uso intensivo. A coordenação pode cancelar reservas.';
  await load();
}

$('login-form').addEventListener('submit', async event => {
  event.preventDefault();
  $('login-submit').disabled = true;
  $('login-message').hidden = true;
  try {
    // Renova o token anônimo inclusive após uma sessão expirar.
    state.csrf = (await api('/auth/sessao')).csrfToken;
    await api('/auth/login', { method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: $('login-email').value, senha: $('login-password').value }) });
    $('login-password').value = '';
    await session();
  } catch (error) {
    $('login-message').textContent = error.message;
    $('login-message').hidden = false;
  } finally { $('login-submit').disabled = false; }
});

document.querySelectorAll('[data-demo]').forEach(button => button.addEventListener('click', () => {
  const admin = button.dataset.demo === 'admin';
  $('login-email').value = admin ? 'admin@senai.demo' : 'professor@senai.demo';
  $('login-password').value = admin ? 'Admin@123' : 'Professor@123';
  $('login-submit').focus();
}));
$('logout').addEventListener('click', async () => {
  $('logout').disabled = true;
  try {
    await api('/auth/logout', { method: 'POST' });
    showLogin();
    await session();
  } catch (error) { $('notice').textContent = error.message; $('notice').hidden = false; }
  finally { $('logout').disabled = false; }
});

function resetEdit(id) {
  const form = $(id);
  delete form.dataset.editingId;
  form.reset();
  form.closest('section').querySelector('h3').textContent = id === 'room-form' ? 'Nova sala' : 'Nova turma';
  form.querySelector('[type="submit"]').textContent = id === 'room-form' ? 'Cadastrar sala ＋' : 'Cadastrar turma ＋';
  form.querySelector('[data-cancel-edit]').hidden = true;
}

function openEdit(id, record) {
  if (!isAdmin()) return;
  const form = $(id);
  if (form.querySelector('fieldset').disabled) return;
  form.reset();
  form.dataset.editingId = record.id;
  for (const [key, value] of Object.entries(record)) {
    const input = form.elements.namedItem(key);
    if (!input) continue;
    if (input.type === 'checkbox') input.checked = Boolean(value);
    else input.value = value ?? '';
  }
  form.closest('section').querySelector('h3').textContent = id === 'room-form' ? 'Editar sala' : 'Editar turma';
  form.querySelector('[type="submit"]').textContent = 'Salvar alterações';
  form.querySelector('[data-cancel-edit]').hidden = false;
  $(id === 'room-form' ? 'room-message' : 'class-message').hidden = true;
  form.closest('section').scrollIntoView({ behavior: window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 'auto' : 'smooth', block: 'start' });
  form.elements.namedItem('nome').focus({ preventScroll: true });
}

function registerForm(id, path, messageId, createPayload, selectId) {
  const cancel = element('button', 'button subtle edit-cancel', 'Cancelar edição');
  cancel.type = 'button';
  cancel.dataset.cancelEdit = '';
  cancel.hidden = true;
  cancel.addEventListener('click', () => { resetEdit(id); $(messageId).hidden = true; });
  $(id).querySelector('fieldset').append(cancel);
  $(id).addEventListener('submit', async event => {
    event.preventDefault();
    const form = event.currentTarget;
    const payload = createPayload(new FormData(form));
    const editingId = form.dataset.editingId;
    const fields = form.querySelector('fieldset');
    fields.disabled = true;
    $(messageId).hidden = true;
    try {
      const created = await api(editingId ? `${path}/${encodeURIComponent(editingId)}` : path,
        { method: editingId ? 'PUT' : 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload) });
      resetEdit(id);
      const refreshed = await load();
      if (refreshed) $(selectId).value = created.id;
      $(messageId).textContent = `${created.nome} ${editingId ? 'atualizada' : 'cadastrada'} com sucesso!${refreshed ? '' : ' Atualize a página para carregar os dados.'}`;
      $(messageId).className = 'form-message success';
    } catch (error) { $(messageId).textContent = error.message; $(messageId).className = 'form-message'; }
    finally { fields.disabled = false; $(messageId).hidden = false; }
  });
}
registerForm('room-form', '/salas', 'room-message', data => ({ nome: data.get('nome'), tipo: data.get('tipo'), capacidade: Number(data.get('capacidade')), localizacao: data.get('localizacao'), altoDesempenho: data.has('altoDesempenho') }), 'room');
registerForm('class-form', '/turmas', 'class-message', data => ({ nome: data.get('nome'), curso: data.get('curso'), turno: data.get('turno'), quantidadeAlunos: Number(data.get('quantidadeAlunos')) }), 'class');

let cancelId = null;
function openCancel(reserva) {
  cancelId = reserva.id;
  $('cancel-description').textContent = `${reserva.atividade} · ${roomName(reserva.salaId)} · ${className(reserva.turmaId)} · ${time(reserva.inicio)}–${time(reserva.fim)}`;
  $('cancel-reason').value = '';
  $('cancel-message').hidden = true;
  $('cancel-dialog').showModal();
  $('cancel-reason').focus();
}
$('cancel-close').addEventListener('click', () => $('cancel-dialog').close());
$('cancel-form').addEventListener('submit', async event => {
  event.preventDefault();
  $('cancel-confirm').disabled = true;
  $('cancel-close').disabled = true;
  try {
    await api(`/reservas/${encodeURIComponent(cancelId)}/cancelar`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ motivo: $('cancel-reason').value.trim() }) });
    $('cancel-dialog').close();
    const refreshed = await load();
    $('notice').textContent = refreshed ? 'Reserva cancelada. O horário está liberado; o registro permanece no histórico.' : 'Reserva cancelada. Atualize a agenda para carregar os dados.';
    $('notice').hidden = false;
  } catch (error) { $('cancel-message').textContent = error.message; $('cancel-message').hidden = false; }
  finally { $('cancel-confirm').disabled = false; $('cancel-close').disabled = false; }
});
session().catch(error => { $('login-message').textContent = error.message; $('login-message').hidden = false; });
