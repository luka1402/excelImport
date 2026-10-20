// Jedini izvor korisničkih poruka (docs/ux-wizard.md, poglavlje 13). Komponente ne sadrže tekst.
export const messages = {
  'app.title': 'Excel Importer',
  'nav.back': 'Nazad',
  'nav.next': 'Dalje',
  'nav.cancel': 'Otkaži',
  'nav.retry': 'Pokušaj ponovo',
  'nav.newImport': 'Novi uvoz',
  'nav.steps': 'Koraci',
  'nav.stepOf': 'Korak {current} od {total} — {title}',
  'nav.history': 'Istorija uvoza',
  'nav.templates': 'Šabloni',
  'common.loading': 'Učitavanje…',
  'common.details': 'Detalji',
  'common.dismiss': 'Zatvori',
  'common.notImplemented': 'Sadržaj ovog koraka biće dodat u narednom zadatku.',
  'common.empty': 'Nema podataka.',
  'common.notFound': 'Stranica nije pronađena.',
  'step.file.title': 'Izaberite Excel fajl',
  'step.file.nav': 'Fajl',
  'step.file.hint': 'Izaberite fajl koji želite da uvezete.',
  'step.analysis.title': 'Analiza fajla',
  'step.analysis.nav': 'Analiza',
  'step.analysis.hint': 'Proverite prepoznate sheet-ove i kolone.',
  'step.target.title': 'Izaberite odredišnu tabelu',
  'step.target.nav': 'Odredište',
  'step.target.hint': 'Izaberite bazu i tabelu u koju se uvozi.',
  'step.mapping.title': 'Povežite kolone',
  'step.mapping.nav': 'Mapiranje',
  'step.mapping.hint': 'Povežite Excel kolone sa kolonama tabele.',
  'step.rules.title': 'Pravila (opciono)',
  'step.rules.nav': 'Pravila',
  'step.rules.hint': 'Dodajte uslove, transformacije i filtere.',
  'step.review.title': 'Provera pre uvoza',
  'step.review.nav': 'Pregled',
  'step.review.hint': 'Ništa se još ne upisuje u bazu.',
  'step.import.title': 'Uvoz',
  'step.import.nav': 'Uvoz',
  'step.import.hint': 'Pratite tok uvoza.',
  'nav.disabledReason.file': 'Izaberite fajl da biste nastavili.',
  'error.unexpected': 'Došlo je do neočekivane greške. Pokušajte ponovo.',
  'error.network': 'Server nije dostupan. Proverite vezu i pokušajte ponovo.',
  'error.status': 'Status',
  'error.traceId': 'Identifikator zahteva',
} as const;

export type MessageKey = keyof typeof messages;

export function t(key: MessageKey, params?: Readonly<Record<string, string | number>>): string {
  const template: string = messages[key];
  if (!params) {
    return template;
  }
  return template.replace(/\{(\w+)\}/g, (placeholder, name: string) => {
    const value = params[name];
    return value === undefined ? placeholder : String(value);
  });
}
