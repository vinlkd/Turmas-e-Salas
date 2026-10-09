import assert from 'node:assert/strict';
const base = process.env.SALAS_TEST_URL || 'http://localhost:5038';
let checks = 0;
function client() {
  const cookies = new Map();
  let csrf = '';
  return {
    async request(path, method = 'GET', body, sendCsrf = true) {
      const response = await fetch(base + path, { method, headers: {
        Cookie: [...cookies].map(([k, v]) => `${k}=${v}`).join('; '),
        ...(body ? { 'Content-Type': 'application/json' } : {}),
        ...(sendCsrf ? { 'X-CSRF-TOKEN': csrf } : {})
      }, body: body ? JSON.stringify(body) : undefined });
      for (const cookie of response.headers.getSetCookie()) {
        const pair = cookie.split(';')[0], index = pair.indexOf('=');
        cookies.set(pair.slice(0, index), pair.slice(index + 1));
      }
      const text = await response.text();
      const data = text ? JSON.parse(text) : {};
      if (path === '/auth/sessao') csrf = data.csrfToken;
      return { status: response.status, data };
    },
    async login(email, senha) {
      await this.request('/auth/sessao');
      const result = await this.request('/auth/login', 'POST', { email, senha });
      if (result.status === 200) await this.request('/auth/sessao');
      return result;
    },
    async save(body, sendCsrf = true) {
      const result = await this.request('/auth/conta', 'PUT', body, sendCsrf);
      if (result.status === 200) await this.request('/auth/sessao');
      return result;
    }
  };
}
function expect(result, code, message) {
  assert.equal(result.status, code, `${message}: ${JSON.stringify(result.data)}`);
  console.log(`OK: ${message}`); checks++; return result.data;
}
const guest = client(), professor = client(), admin = client();
assert.equal((await guest.request('/auth/sessao')).data.demonstracao, true, 'Use somente dados isolados de demonstração.');
expect(await guest.request('/auth/conta'), 401, 'Conta exige login');
expect(await professor.login('professor@senai.demo', 'Professor@123'), 200, 'Login de professor');
expect(await admin.login('admin@senai.demo', 'Admin@123'), 200, 'Login de coordenação');
const original = expect(await professor.request('/auth/conta'), 200, 'Professor consulta os próprios dados');
assert.equal(original.email, 'professor@senai.demo');
assert.ok(!('senhaHash' in original));
expect(await professor.save({ nome: 'Professor atualizado', email: original.email }, false), 400, 'Edição exige CSRF');
expect(await professor.save({ nome: 'Professor atualizado', email: 'email-invalido' }), 400, 'E-mail inválido recusado');
expect(await professor.save({ nome: 'Professor atualizado', email: 'novo@senai.demo' }), 400, 'Alteração de e-mail exige senha atual');
expect(await professor.save({ nome: 'Professor atualizado', email: original.email, novaSenha: '123' }), 400, 'Senha curta recusada');
expect(await professor.save({ nome: 'Professor atualizado', email: 'novo@senai.demo', senhaAtual: 'errada' }), 400, 'Senha atual incorreta recusada');
expect(await professor.save({ nome: 'Professor atualizado', email: 'admin@senai.demo', senhaAtual: 'Professor@123' }), 409, 'E-mail de outra conta recusado');
const nameChanged = expect(await professor.save({ nome: 'Professor atualizado', email: original.email, id: 'admin-demo', perfil: 'Admin' }), 200, 'Nome editado sem alterar perfil ou ID');
assert.equal(nameChanged.id, original.id);
assert.equal(nameChanged.perfil, 'Professor');
assert.equal((await professor.request('/auth/sessao')).data.usuario.nome, 'Professor atualizado');
assert.equal((await admin.request('/auth/conta')).data.nome, 'Coordenação SENAI');
const email = `conta-${Date.now()}@senai.demo`;
expect(await professor.save({ nome: 'Professor atualizado', email, senhaAtual: 'Professor@123', novaSenha: 'NovaSenha@123' }), 200, 'Professor altera e-mail e senha');
expect(await professor.request('/auth/logout', 'POST'), 204, 'Logout funciona');
expect(await professor.login(original.email, 'Professor@123'), 401, 'Credenciais antigas deixam de funcionar');
expect(await professor.login(email, 'NovaSenha@123'), 200, 'Novas credenciais funcionam');
expect(await professor.save({ nome: original.nome, email: original.email, senhaAtual: 'NovaSenha@123', novaSenha: 'Professor@123' }), 200, 'Restauração dos dados do professor');
const adminProfile = expect(await admin.request('/auth/conta'), 200, 'Coordenação consulta a própria conta');
const adminChanged = expect(await admin.save({ nome: 'Coordenação atualizada', email: adminProfile.email }), 200, 'Coordenação também edita seus dados');
assert.equal(adminChanged.perfil, 'Admin');
assert.equal((await admin.request('/auth/sessao')).data.usuario.nome, 'Coordenação atualizada');
expect(await admin.save({ nome: adminProfile.nome, email: adminProfile.email }), 200, 'Restauração dos dados da coordenação');
console.log(`${checks} verificações da conta passaram.`);
