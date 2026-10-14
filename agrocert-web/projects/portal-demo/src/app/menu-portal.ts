export interface ItemPortal {
  icone: string;
  texto: string;
  caminho: string;
  funcionalidade: string;
  descricao: string;
}

const item = (icone: string, texto: string, caminho: string, descricao: string, funcionalidade?: string): ItemPortal =>
  ({ icone, texto, caminho, descricao, funcionalidade: funcionalidade ?? texto.charAt(0) + texto.slice(1).toLowerCase() });

export const MEU_ESTOQUE = item('inventory', 'Meu Estoque', 'portal/meu-estoque',
  'consultar o saldo dos seus produtos armazenados na cooperativa a qualquer momento.', 'Meu estoque');
export const MINHAS_VENDAS = item('request_quote', 'Minhas Vendas', 'portal/minhas-vendas',
  'acompanhar as suas vendas, ordens e pagamentos em um só lugar.', 'Minhas vendas');

export const PLANEJAMENTO: ItemPortal[] = [
  item('eco', 'Planejamento de Insumos', 'portal/planejamento-insumos',
    'organizar e acompanhar o agendamento dos seus insumos com mais eficiência.', 'Planejamento de insumos'),
  item('agriculture', 'Planejamento de Plantio', 'portal/planejamento-plantio',
    'planejar as áreas, as culturas e as datas de plantio de cada safra.', 'Planejamento de plantio'),
  item('calendar_today', 'Planejamento de Colheita', 'portal/planejamento-colheita',
    'programar a colheita e a entrega da sua produção na cooperativa.', 'Planejamento de colheita'),
  item('donut_small', 'Produção Média por Cultura', 'portal/producao-media',
    'comparar a produtividade média de cada cultura ao longo das safras.', 'Produção média por cultura'),
  item('spa', 'Safras', 'portal/safras',
    'consultar o histórico e os resultados de cada safra.', 'Safras'),
];

export const RELATORIOS = item('insert_chart', 'Relatórios', 'portal/relatorios', '', 'Relatórios');

export const LISTA_RELATORIOS: ItemPortal[] = [
  ['AGR001', 'Análise de Solo', 'consultar os resultados das análises de solo por talhão e safra.'],
  ['ATC001', 'Análise de Potencial', 'avaliar o potencial produtivo das suas áreas.'],
  ['ATC002', 'Planejamento Agrícola', 'acompanhar o planejamento agrícola por cultura e safra.'],
  ['COOP001', 'Portal do Cooperado', 'acompanhar os indicadores de uso do portal pelos cooperados.'],
  ['EST001', 'Gestão de Estoque por Lote.', 'consultar o estoque separado por lote.'],
  ['EST002', 'Gestão de Estoque', 'acompanhar a posição de estoque da cooperativa.'],
  ['EST003', 'Gestão de Estoque de Transferência', 'acompanhar as transferências de estoque entre unidades.'],
  ['SD001', 'Faturamento Gerencial', 'acompanhar o faturamento por período, produto e cliente.'],
  ['SD003', 'Gestão de Venda Futura e Antecipada', 'acompanhar os contratos de venda futura e antecipada.'],
  ['SD004', 'Exportação Hass', 'acompanhar as exportações de abacate Hass.'],
  ['SUP001', 'Indicadores de Suprimentos', 'acompanhar os indicadores de compras e suprimentos.'],
].map(([codigo, nome, descricao]) => item('insert_chart', `${codigo} - ${nome}`, `portal/relatorios/${codigo.toLowerCase()}`, descricao, `${codigo} - ${nome}`));
export const ROMANEIO = item('assignment', 'Romaneio', 'portal/romaneio',
  'consultar os romaneios de entrega da sua produção.', 'Romaneio');

export const SERVICOS: ItemPortal[] = [
  item('terrain', 'Amostragem de Solo', 'portal/amostragem-solo',
    'solicitar e acompanhar a coleta e os resultados das análises de solo.', 'Amostragem de solo'),
  item('inventory_2', 'Armazenagem', 'portal/armazenagem',
    'acompanhar os serviços de armazenagem da sua produção.', 'Armazenagem'),
  item('category', 'Beneficiamento', 'portal/beneficiamento',
    'acompanhar o beneficiamento da sua produção na cooperativa.', 'Beneficiamento'),
  item('science', 'Exame Laboratorial', 'portal/exame-laboratorial',
    'solicitar exames laboratoriais e consultar os laudos.', 'Exame laboratorial'),
  item('local_shipping', 'Logística', 'portal/logistica',
    'agendar e acompanhar o transporte da sua produção e dos seus insumos.', 'Logística'),
  item('pest_control', 'Monitoramento de Praga', 'portal/monitoramento-praga',
    'registrar e acompanhar o monitoramento de pragas nas suas lavouras.', 'Monitoramento de praga'),
  item('water_drop', 'Pivô', 'portal/pivo',
    'acompanhar os serviços e a manutenção dos pivôs de irrigação.', 'Pivô'),
  item('opacity', 'Pulverização', 'portal/pulverizacao',
    'solicitar e acompanhar os serviços de pulverização.', 'Pulverização'),
];

export const VISITA_TECNICA = item('perm_contact_calendar', 'Visita Técnica', 'portal/visita-tecnica', '', 'Visita técnica');

export const COMUNICACAO: ItemPortal[] = [
  item('contact_mail', 'Fale Conosco', 'portal/fale-conosco', 'enviar dúvidas e sugestões direto para a cooperativa.', 'Fale conosco'),
  item('newspaper', 'Notícias', 'portal/noticias', 'acompanhar as notícias e os comunicados da cooperativa.', 'Notícias'),
];

export const TODOS_EM_BREVE: ItemPortal[] = [
  MEU_ESTOQUE, MINHAS_VENDAS, ...PLANEJAMENTO, ...LISTA_RELATORIOS, ROMANEIO, ...SERVICOS, ...COMUNICACAO,
];
