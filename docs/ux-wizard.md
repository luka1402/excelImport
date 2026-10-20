# UX čarobnjaka — Excel Importer (COA-17 / T02)

Tekstualni wireframe-ovi za FE (T12, T15, T18, T23, T24, T29, T32, T33). Svi tekstovi su na srpskom (latinica).
Oznake: `[Dugme]` dugme, `[____]` polje, `(•)` radio, `[x]` checkbox, `[v]` padajuća lista, `⚠` upozorenje, `✖` greška, `✔` uspeh.

## 1. Opšti okvir

### 1.1 Raspored (desktop ≥ 1024 px)
```
+--------------------------------------------------------------------------+
| Excel Importer                              [Istorija uvoza] [Šabloni]   |
+--------------------------------------------------------------------------+
| (1)Fajl─(2)Analiza─(3)Odredište─(4)Mapiranje─(5)Pravila─(6)Pregled─(7)Uvoz|
|   ✔        ✔           ●            ○           ○          ○        ○    |
+--------------------------------------------------------------------------+
|  <h1>Naslov koraka</h1>                                                  |
|  Kratko uputstvo (jedna rečenica).                                       |
|                                                                          |
|  [ sadržaj koraka ]                                                      |
|                                                                          |
+--------------------------------------------------------------------------+
| [← Nazad]                         [Sačuvaj kao šablon]     [Dalje →]     |
+--------------------------------------------------------------------------+
```
Korak 8 (šabloni) nije poseban ekran u toku, već: izbor šablona na početku (korak 1), dugme „Sačuvaj kao šablon" u podnožju od koraka 4 nadalje, i ekran „Šabloni" u zaglavlju.

### 1.2 Pravila navigacije
- **Nazad** je dostupan u svakom koraku osim u prvom; vraća na prethodni korak **bez gubitka unetih podataka** (stanje čarobnjaka živi u jednom provajderu iznad koraka).
- Klik na već završen korak u traci koraka vraća na njega. Koraci ispred trenutnog nisu klikabilni (`aria-disabled`).
- Promena u ranijem koraku koja poništava kasnije izbore (novi fajl, drugi sheet/header red, druga tabela) traži potvrdu:
  > „Promena će poništiti mapiranje i pravila iz narednih koraka. Nastaviti?" `[Otkaži]` `[Nastavi]`
  - Promena fajla/sheet-a/header-a briše mapiranje i pravila; promena tabele briše mapiranje i pravila; promena samo pravila ne briše ništa.
- **Dalje** je onemogućeno dok korak nije validan; ispod dugmeta je uvek vidljiv razlog (tekst, ne samo siva boja).
- Nakon starta uvoza (korak 7) **Nazad** je isključen; nudi se samo „Novi uvoz".
- Osvežavanje stranice u koracima 1–6 vraća na korak 1 (fajl nije sačuvan na klijentu); u koraku 7 stanje se obnavlja prema `jobId` iz URL-a.
- URL: `/uvoz/:korak` (`fajl`, `analiza`, `odredište`, `mapiranje`, `pravila`, `pregled`, `uvoz/:jobId`), `/istorija`, `/istorija/:jobId`, `/šabloni`.

### 1.3 Zajednički obrasci stanja (važe za svaki ekran)
| Stanje | Prikaz |
|---|---|
| Učitavanje | Skeleton ili spinner sa tekstom (`role="status"`, `aria-live="polite"`); dugme „Dalje" onemogućeno. Operacije duže od 3 s dobijaju traku napretka. |
| Greška | Okvir `role="alert"` sa naslovom, razlogom na srpskom i dugmetom `[Pokušaj ponovo]`; tehnički detalji (status, `traceId`) u rasklopivom „Detalji". Nikad connection string, putanje ni stack trace. |
| Prazno | Ilustracija/ikonica + objašnjenje + glavna akcija (npr. „Izaberi fajl"). |

```
 Učitavanje              Greška                               Prazno
+------------------+   +----------------------------------+  +---------------------+
|  ◌ Učitavanje... |   | ✖ Naslov greške                  |  |        (ikona)      |
|  ▒▒▒▒▒▒▒▒░░░░    |   |   Razlog na srpskom.             |  |  Nema podataka.     |
+------------------+   |   [Pokušaj ponovo]  ▸ Detalji    |  |  [Glavna akcija]    |
                       +----------------------------------+  +---------------------+
```

---

## 2. Korak 1 — Upload fajla (T12)

### Podrazumevano
```
+--------------------------------------------------------------------------+
| Korak 1 od 7 — Izaberite Excel fajl                                      |
|                                                                          |
|  Početi iz šablona (opciono):  [v Bez šablona                        ]   |
|                                                                          |
|  +--------------------------------------------------------------------+  |
|  |        ⇪  Prevucite fajl ovde ili [Izaberite fajl]                 |  |
|  |        Dozvoljeno: .xlsx, .xls — najviše 50 MB                     |  |
|  +--------------------------------------------------------------------+  |
|                                                                          |
|  Izabrani fajl: prodaja_2026.xlsx (2,3 MB)   [Ukloni]                    |
|  Otpremanje: ▓▓▓▓▓▓▓▓░░ 78 %                    [Otkaži]                 |
+--------------------------------------------------------------------------+
| [← Nazad (onemogućeno)]                                [Dalje → ]        |
+--------------------------------------------------------------------------+
```
- **Učitavanje:** traka napretka otpremanja sa procentom; zona za prevlačenje onemogućena; „Otkaži" prekida zahtev.
- **Greška** (klijentska provera pre slanja i serverska posle):
  - „Fajl nije u podržanom formatu. Dozvoljeni su .xlsx i .xls."
  - „Fajl je prevelik (najviše 50 MB)."
  - „Fajl je prazan."
  - „Fajl ne može da se pročita. Moguće je da je oštećen ili zaštićen lozinkom."
  - „Otpremanje nije uspelo. Proverite vezu i pokušajte ponovo."
- **Prazno:** nijedan fajl izabran → zona za prevlačenje + tekst „Još niste izabrali fajl." „Dalje" onemogućeno sa razlogom „Izaberite fajl da biste nastavili."

---

## 3. Korak 2 — Analiza fajla (T12)

```
+--------------------------------------------------------------------------+
| Korak 2 od 7 — Analiza fajla                                             |
|  Sheet: [v Prodaja        ]     Red sa nazivima kolona: [ 1 ] (automatski)|
|  ⓘ Header je prepoznat u redu 1.                    [Promeni red]        |
|                                                                          |
|  Kolone                                                                  |
|  +----+-------------+---------------+------------+                       |
|  | #  | Naziv       | Tip           | Prazno     |                       |
|  | A  | Kupac       | Tekst         | 0 %        |                       |
|  | B  | Količina    | Ceo broj      | 2 %        |                       |
|  | C  | Datum       | Datum         | 0 %        |                       |
|  +----+-------------+---------------+------------+                       |
|  Pregled prvih 20 redova (horizontalno skrolovanje na uskim ekranima)    |
|  +--------+----------+------------+                                      |
|  | Kupac  | Količina | Datum      |                                      |
|  | Petrović d.o.o. | 12 | 03.02.2026 |                                   |
|  +--------+----------+------------+     Ukupno redova: 12.480            |
+--------------------------------------------------------------------------+
| [← Nazad]                                                [Dalje →]       |
+--------------------------------------------------------------------------+
```
- Tip kolone je prepoznat automatski; korisnik ga može **samo pregledati** (izmena tipa nije u obimu).
- Ručna promena header reda: broj polja (1…20), posle promene se tabela ponovo učitava.
- **Učitavanje:** skeleton tabele + „Analiziram fajl…".
- **Greška:** „Analiza fajla nije uspela. Pokušajte ponovo ili izaberite drugi fajl." `[Pokušaj ponovo]` `[Izaberi drugi fajl]`.
- **Prazno:**
  - Sheet bez podataka: „Izabrani sheet je prazan. Izaberite drugi sheet." (ostali sheet-ovi ostaju u izboru).
  - Fajl bez ijednog sheet-a sa podacima: „Fajl ne sadrži podatke." `[Izaberi drugi fajl]`.
- Upozorenje: „⚠ Naziv kolone se ponavlja: „Cena" (kolone C i F)." i „⚠ Kolona D nema naziv."

---

## 4. Korak 3 — Izbor odredišta (T15)

```
+--------------------------------------------------------------------------+
| Korak 3 od 7 — Izaberite odredišnu tabelu                                |
|  Baza:   [v Demo baza                 ]                                  |
|  Tabela: [v dbo.Prodaja               ]   (pretraga kucanjem)            |
|                                                                          |
|  Kolone tabele dbo.Prodaja                                               |
|  +------------+-----------------+----------+----------+-----------+     |
|  | Kolona     | Tip             | Obavezno | Ključ    | Dužina    |     |
|  | Id         | int             | Da       | PK       | —         |     |
|  | Kupac      | nvarchar        | Da       | —        | 100       |     |
|  | GradId     | int             | Ne       | FK → Grad| —         |     |
|  +------------+-----------------+----------+----------+-----------+     |
+--------------------------------------------------------------------------+
| [← Nazad]                                                [Dalje →]       |
+--------------------------------------------------------------------------+
```
- Lista baza dolazi sa servera kao ID + prikazni naziv; connection string se nikad ne prikazuje.
- **Učitavanje:** „Učitavam listu baza…", pa „Učitavam tabele…" (skeleton liste kolona).
- **Greška:** „Baza trenutno nije dostupna. Proverite da je servis pokrenut i pokušajte ponovo." / „Šema baze ne može da se pročita." `[Pokušaj ponovo]`. Ako pad nastane usred rada: traka na vrhu, izbor ostaje, „Dalje" onemogućeno.
- **Prazno:** „Nema podešenih baza. Obratite se administratoru." / „Baza ne sadrži nijednu tabelu u koju je dozvoljen uvoz."; pretraga bez rezultata: „Nijedna tabela ne odgovara pretrazi „xyz"."

---

## 5. Korak 4 — Mapiranje kolona (T18)

```
+--------------------------------------------------------------------------+
| Korak 4 od 7 — Povežite kolone                                           |
|  ✔ Predloženo mapiranje za 3 od 4 kolone. [Prihvati sve predloge]        |
|                                                                          |
|  Excel kolona        →   Ciljna kolona                    Upozorenje     |
|  Kupac (tekst)       →   [v Kupac (nvarchar 100)  ]       ⚠ Tekst može  |
|                                                              biti duži   |
|  Količina (ceo broj) →   [v Kolicina (int)        ]       —              |
|  Napomena (tekst)    →   [v — Preskoči —          ]       —              |
|                                                                          |
|  Nemapirane ciljne kolone                                                |
|  GradId (int, nije obavezno)        — ostaje prazno                      |
|  Status (nvarchar, OBAVEZNO)  ✖ [ Konstanta: (•) Vrednost [Aktivan___]   |
|                                         ( ) Današnji datum/vreme ]       |
+--------------------------------------------------------------------------+
| [← Nazad]                       [Sačuvaj kao šablon]     [Dalje →]       |
+--------------------------------------------------------------------------+
```
- Predlozi su unapred popunjeni, vizuelno označeni („Predlog") dok ih korisnik ne potvrdi ili promeni.
- Jedna ciljna kolona se ne može izabrati dvaput (zauzete opcije su prikazane sivo sa „već mapirano").
- Upozorenja po koloni, uvek tekst + ikona:
  - „⚠ Tip se ne poklapa: Excel „tekst", baza „int"."
  - „⚠ Obavezna kolona „Status" nije mapirana. Mapirajte je ili unesite konstantu."
  - „⚠ Tekst može biti duži od dozvoljenih 100 znakova."
- Obavezna nemapirana kolona **blokira „Dalje"** dok se ne mapira ili ne dobije konstantu.
- Konstanta: tekstualno polje ili „Današnji datum/vreme" (računa se u trenutku uvoza); neodgovarajući tip → „Vrednost ne odgovara tipu kolone (ceo broj)."
- **Nazad/napred čuva izbor** (vidi 1.2).
- **Učitavanje:** „Pripremam predlog mapiranja…" (skeleton redova).
- **Greška:** „Predlog mapiranja nije mogao da se napravi. Možete mapirati kolone ručno." `[Pokušaj ponovo]` — ručno mapiranje ostaje dostupno.
- **Prazno:** Excel nema kolona: „U fajlu nema kolona za mapiranje. Vratite se na korak 2."; nijedna kolona nije mapirana: „Nijedna kolona nije povezana. Prihvatite predlog ili izaberite ciljne kolone." (Dalje onemogućeno).

---

## 6. Korak 5 — Uslovi i pravila (T23, T24)

```
+--------------------------------------------------------------------------+
| Korak 5 od 7 — Pravila (opciono)                                         |
|  +-------------------+  Kolona: Status                                   |
|  | Kolone            |  Da li postoji uslov?  (•) Ne  ( ) Da             |
|  | > Kupac      (1)  |                                                   |
|  |   Količina        |  Pravila za ovu kolonu (redosled primene):        |
|  |   Status     (2)  |  1. Transformacija: Ukloni razmake   [Izmeni][✕]  |
|  |   Grad       (1)  |  2. Ako Status = „Da" onda 1, inače 0 [Izmeni][✕] |
|  |-------------------|  [+ Dodaj pravilo ▾]                              |
|  | Filter redova (0) |     Uslovna vrednost / Podrazumevana vrednost /   |
|  +-------------------+     Transformacija / Lookup                       |
+--------------------------------------------------------------------------+
| [← Nazad]                       [Sačuvaj kao šablon]     [Dalje →]       |
+--------------------------------------------------------------------------+
```
Na uskim ekranima lista kolona postaje padajuća lista iznad forme.

### Forme
```
Uslovna vrednost                          Podrazumevana vrednost
 Ako [v Količina] [v je veće od] [__]      Ako je ćelija prazna upiši: [______]
   (+ I još uslov)  onda [______]
   U suprotnom [______] (opciono)

Transformacija                            Lookup (strani ključ)
 [v Ukloni razmake | Velika slova |        Vrednost iz ćelije traži u tabeli [v dbo.Grad]
    Mala slova | Format datuma |           po koloni [v Naziv] i upiši [v Id]
    Decimalni separator ]                  ⓘ Pretraga ne razlikuje velika/mala slova.
 Format datuma: [dd.MM.yyyy]
 Decimalni separator: ( ) , ( ) .

Filter redova
 Uvezi samo redove gde: [v Količina] [v je veće od] [0]  (+ Dodaj uslov)
 Preskoči redove gde:   [v Status]  [v je jednako] [Obrisan]
```
Operatori (srpski nazivi): je jednako, nije jednako, veće od, manje od, veće ili jednako, manje ili jednako, sadrži, počinje sa, je prazno, nije prazno. Za „je prazno / nije prazno" polje vrednosti se sakriva.

- Lookup nudi samo tabele i kolone iz učitane šeme (padajuće liste, bez slobodnog unosa).
- Validacija formi na srpskom: „Unesite vrednost.", „Izaberite kolonu.", „Vrednost mora biti broj.", „Format datuma nije ispravan."
- Brisanje pravila: potvrda „Obrisati pravilo?" `[Otkaži]` `[Obriši]`.
- **Učitavanje:** skeleton forme dok se učitavaju tabele/kolone za lookup.
- **Greška:** „Lista tabela za lookup nije dostupna." `[Pokušaj ponovo]`; „Tabela „X" više ne postoji u izabranoj bazi. Izaberite drugu." (npr. posle promene konekcije).
- **Prazno:** „Za ovu kolonu nema pravila. Pravila nisu obavezna — možete nastaviti." `[+ Dodaj pravilo]`.

---

## 7. Korak 6 — Pregled (dry run) (T29)

```
+--------------------------------------------------------------------------+
| Korak 6 od 7 — Provera pre uvoza                                         |
|  Ništa se još ne upisuje u bazu.                                         |
|  +-------------+ +-------------+ +-------------+                         |
|  | ✔ Za uvoz   | | ➜ Preskočeno| | ✖ Sa greškom|                         |
|  |   12.300    | |     150     | |      30     |                         |
|  +-------------+ +-------------+ +-------------+                         |
|                                                                          |
|  Greške (prikazano prvih 100 od 30)                                      |
|  +------+-----------+-------------------------------------+              |
|  | Red  | Kolona    | Razlog                              |              |
|  | 15   | Datum     | „31.02.2026" nije ispravan datum.   |              |
|  | 22   | Grad      | Grad „Nš" nije pronađen.            |              |
|  +------+-----------+-------------------------------------+              |
|                                                                          |
|  Pregled prvih 20 redova posle transformacije  [tabela]                  |
|                                                                          |
|  Način uvoza:  (•) Sve ili ništa — ako ijedan red ima grešku, ništa se   |
|                    ne upisuje.                                           |
|                ( ) Preskoči neispravne — ispravni se upisuju, neispravni |
|                    se vraćaju u izveštaj.                                |
+--------------------------------------------------------------------------+
| [← Nazad]                                      [Pokreni uvoz]            |
+--------------------------------------------------------------------------+
```
- Ako ima grešaka a izabrano je „Sve ili ništa": žuta napomena „⚠ Uvoz će biti odbijen jer postoje redovi sa greškom." i dugme `[Pokreni uvoz]` ostaje dostupno (korisnik odlučuje) ali sa potvrdom.
- Pre pokretanja: potvrda „Upisati 12.300 redova u tabelu dbo.Prodaja?" `[Otkaži]` `[Pokreni uvoz]`.
- **Učitavanje:** traka „Proveravam redove… 45 %" (dry run na velikom fajlu traje); `[Otkaži proveru]`.
- **Greška:** „Provera nije uspela." + razlog; `[Pokušaj ponovo]` `[← Nazad]`.
- **Prazno:** nijedan red za uvoz: „Nijedan red nije spreman za uvoz. Proverite pravila i mapiranje." (`[Pokreni uvoz]` onemogućeno). Nema grešaka: „✔ Nisu pronađene greške."

---

## 8. Korak 7 — Uvoz i izveštaj (T29)

### U toku
```
+--------------------------------------------------------------------------+
| Korak 7 od 7 — Uvoz u toku                                               |
|  ▓▓▓▓▓▓▓▓▓▓░░░░░░░░  52 %   (6.400 od 12.300 redova)                     |
|  Proteklo vreme: 00:08                              [Otkaži uvoz]        |
|  Uvoz se nastavlja i ako zatvorite stranicu. Stanje možete pratiti u      |
|  istoriji.                                                               |
+--------------------------------------------------------------------------+
```

### Završeno
```
+--------------------------------------------------------------------------+
| ✔ Uvoz je završen                    (ili ⚠ Završen sa greškama)         |
|  Uvezeno: 12.300     Preskočeno: 150     Greške: 30    Trajanje: 00:14   |
|  [Preuzmi izveštaj o greškama (.xlsx)]  [Sačuvaj kao šablon] [Novi uvoz] |
|  [Prikaži u istoriji]                                                    |
+--------------------------------------------------------------------------+
```
Statusi i boje (uvek uz tekst i ikonu): Završen ✔, Završen sa greškama ⚠, Neuspeo ✖, Otkazan ⊘.

- **Učitavanje:** prvo „Pokrećem uvoz…" do dobijanja `jobId`; zatim polling (npr. na 1 s) sa `aria-live="polite"` ažuriranjem procenta na svakih 10 %.
- **Greška:**
  - „Uvoz nije uspeo. Nijedan red nije upisan." (Sve ili ništa)
  - „Uvoz je prekinut. Upisano je 6.400 redova." (Preskoči neispravne, pad u toku)
  - „Status uvoza trenutno nije dostupan. Pokušavam ponovo…" (privremeni prekid pollinga; automatski retry)
  - „Uvoz je otkazan." (posle otkazivanja, uz broj upisanih redova za režim „Preskoči")
  - Izveštaj: „Izveštaj nije dostupan ili je istekao."
- **Prazno:** uvoz završen sa 0 redova: „Nijedan red nije upisan." Bez grešaka dugme izveštaja je sakriveno.
- Osvežavanje stranice: ekran se obnavlja iz `jobId`.

---

## 9. Korak 8 — Šabloni (T32)

### Izbor šablona na početku (korak 1)
```
 Početi iz šablona: [v Bez šablona | Prodaja mesečno | Kupci uvoz ]
```
Posle analize fajla (korak 2) šablon se primenjuje automatski; ako se kolone razlikuju, traka na vrhu koraka 4:
```
 ⚠ Šablon „Prodaja mesečno" ne odgovara potpuno fajlu.
   Nove kolone u fajlu: Popust.   Kolone koje fajl nema: Napomena.
   [Razumem]
```
Isti fajl ⇒ bez upozorenja.

### Sačuvaj kao šablon (dijalog)
```
+------------------------------------------+
| Sačuvaj kao šablon                  [✕]  |
|  Naziv: [____________________]  (obavezno)|
|  Čuva mapiranje, pravila i konstante.    |
|           [Otkaži]  [Sačuvaj]            |
+------------------------------------------+
```
Poruke: „Naziv je obavezan.", „Šablon sa tim nazivom već postoji.", „Šablon je sačuvan.", „Šablon se odnosi na kolonu „X" koja više ne postoji u bazi."

### Ekran „Šabloni"
```
 Šabloni                                    [Pretraga ____]
 +--------------------+--------------+---------------+----------+
 | Naziv              | Tabela       | Izmenjen      | Akcije   |
 | Prodaja mesečno    | dbo.Prodaja  | 12.10.2026.   | [Obriši] |
 +--------------------+--------------+---------------+----------+
```
- **Učitavanje:** skeleton tabele. **Greška:** „Šabloni nisu mogli da se učitaju." `[Pokušaj ponovo]`. **Prazno:** „Još nema sačuvanih šablona. Šablon možete sačuvati u koraku 4 ili posle uvoza."
- Brisanje: potvrda „Obrisati šablon „Prodaja mesečno"? Ovo se ne može poništiti."

---

## 10. Istorija uvoza (T33)

```
+--------------------------------------------------------------------------+
| Istorija uvoza     Status: [v Svi]  Od: [__.__.____]  Do: [__.__.____]   |
| +-----------------+----------------+-------------+--------+------------+ |
| | Datum           | Fajl           | Tabela      | Šablon | Status     | |
| | 20.10.2026 9:41 | prodaja.xlsx   | dbo.Prodaja | —      | ✔ Završen  | |
| |  Uvezeno 12.300 · Preskočeno 150 · Greške 30                          | |
| +-----------------+----------------+-------------+--------+------------+ |
| Prikazano 1–20 od 134            [← Prethodna] [1][2][3] [Sledeća →]     |
+--------------------------------------------------------------------------+
```
Detalji (`/istorija/:jobId`): zaglavlje (fajl, tabela, režim, trajanje), brojevi, tabela grešaka (do 10.000 sačuvanih; poruka „Prikazano je prvih 10.000 grešaka. Kompletna lista je u izveštaju."), `[Preuzmi izveštaj (.xlsx)]`, `[← Nazad na istoriju]`.
- Sortiranje po datumu opadajuće (podrazumevano); stranica 20 redova.
- **Učitavanje:** skeleton redova. **Greška:** „Istorija nije mogla da se učita." `[Pokušaj ponovo]`; „Uvoz nije pronađen." (detalji). **Prazno:** „Još nema izvršenih uvoza." `[Započni uvoz]`; filter bez rezultata: „Nijedan uvoz ne odgovara filterima." `[Poništi filtere]`.

---

## 11. Responsive ponašanje
| Širina | Ponašanje |
|---|---|
| ≥ 1024 px | Raspored kao u 1.1; dvokolonski raspored u koraku 5. |
| 640–1023 px | Traka koraka skraćena na brojeve + naziv trenutnog koraka; tabele u kontejneru sa horizontalnim skrolovanjem. |
| < 640 px | Traka koraka = „Korak 4 od 7 — Mapiranje" sa `<progress>`; podnožje sa dugmadima lepljivo (sticky) pri dnu, dugmad preko cele širine (Nazad ispod Dalje); mapiranje se prikazuje kao kartice (jedna Excel kolona = jedna kartica); tabele pregleda skrolovane horizontalno sa fiksnom prvom kolonom; ciljevi dodira ≥ 44×44 px. |

## 12. Pristupačnost (WCAG 2.2 AA)
- **Tastatura:** sve radnje dostupne bez miša; logičan redosled Tab; vidljiv fokus (≥ 2 px, kontrast ≥ 3:1). Zona za upload je dugme/`<input type="file">` dostupan tastaturom (Enter/Space), prevlačenje je samo dodatak. Dijalozi hvataju fokus, Esc zatvara, fokus se vraća na okidač. Padajuće liste su nativni `<select>` ili ARIA combobox sa strelicama.
- **Labele:** svako polje ima vidljivu `<label for>`; greške polja povezane preko `aria-describedby`, `aria-invalid="true"`; obavezna polja imaju tekst „(obavezno)", ne samo zvezdicu.
- **Struktura:** jedan `<h1>` po koraku (naslov koraka), `<nav aria-label="Koraci">` sa `<ol>` i `aria-current="step"` na trenutnom; tabele sa `<caption>`, `<th scope>`; semantički `<button>`/`<a>`.
- **Fokus pri promeni koraka:** fokus ide na `<h1>` novog koraka (`tabindex="-1"`), a naslov dokumenta se ažurira („Korak 4 od 7 — Mapiranje").
- **Obaveštenja:** greške `role="alert"`; učitavanje i progres `role="status"` / `aria-live="polite"`, `<progress>` ili `role="progressbar"` sa `aria-valuenow`.
- **Kontrast i boje:** tekst ≥ 4.5:1, krupan tekst i elementi UI ≥ 3:1; status se nikad ne prenosi samo bojom (uvek ikona + tekst: ✔ ⚠ ✖ ⊘).
- **Pokret:** poštovati `prefers-reduced-motion` (bez animacija trake/skeleton sjaja).
- **Zoom/tekst:** radi do 200 % zumiranja bez horizontalnog skrolovanja stranice (osim tabela podataka); jedinice `rem`.
- **Jezik:** `<html lang="sr-Latn">`.

## 13. Katalog poruka (srpski)
Centralizovati u jednom fajlu (npr. `src/messages.sr.ts`), bez teksta u komponentama.

| Ključ | Tekst |
|---|---|
| `nav.back` | Nazad |
| `nav.next` | Dalje |
| `nav.cancel` | Otkaži |
| `nav.retry` | Pokušaj ponovo |
| `nav.newImport` | Novi uvoz |
| `nav.confirmReset` | Promena će poništiti mapiranje i pravila iz narednih koraka. Nastaviti? |
| `nav.disabledReason.file` | Izaberite fajl da biste nastavili. |
| `common.loading` | Učitavanje… |
| `common.requiredField` | (obavezno) |
| `common.details` | Detalji |
| `upload.dropzone` | Prevucite fajl ovde ili izaberite fajl |
| `upload.hint` | Dozvoljeno: .xlsx, .xls — najviše 50 MB |
| `upload.err.format` | Fajl nije u podržanom formatu. Dozvoljeni su .xlsx i .xls. |
| `upload.err.size` | Fajl je prevelik (najviše 50 MB). |
| `upload.err.empty` | Fajl je prazan. |
| `upload.err.unreadable` | Fajl ne može da se pročita. Moguće je da je oštećen ili zaštićen lozinkom. |
| `upload.err.network` | Otpremanje nije uspelo. Proverite vezu i pokušajte ponovo. |
| `analysis.loading` | Analiziram fajl… |
| `analysis.headerDetected` | Header je prepoznat u redu {row}. |
| `analysis.err.generic` | Analiza fajla nije uspela. Pokušajte ponovo ili izaberite drugi fajl. |
| `analysis.empty.sheet` | Izabrani sheet je prazan. Izaberite drugi sheet. |
| `analysis.empty.file` | Fajl ne sadrži podatke. |
| `analysis.warn.duplicateHeader` | Naziv kolone se ponavlja: „{name}". |
| `analysis.warn.noHeaderName` | Kolona {letter} nema naziv. |
| `target.loadingDbs` | Učitavam listu baza… |
| `target.loadingTables` | Učitavam tabele… |
| `target.err.dbDown` | Baza trenutno nije dostupna. Proverite da je servis pokrenut i pokušajte ponovo. |
| `target.err.schema` | Šema baze ne može da se pročita. |
| `target.empty.dbs` | Nema podešenih baza. Obratite se administratoru. |
| `target.empty.tables` | Baza ne sadrži nijednu tabelu u koju je dozvoljen uvoz. |
| `target.empty.search` | Nijedna tabela ne odgovara pretrazi „{query}". |
| `mapping.suggested` | Predloženo mapiranje za {n} od {total} kolona. |
| `mapping.acceptAll` | Prihvati sve predloge |
| `mapping.skip` | — Preskoči — |
| `mapping.alreadyMapped` | već mapirano |
| `mapping.warn.type` | Tip se ne poklapa: Excel „{from}", baza „{to}". |
| `mapping.warn.required` | Obavezna kolona „{name}" nije mapirana. Mapirajte je ili unesite konstantu. |
| `mapping.warn.length` | Tekst može biti duži od dozvoljenih {max} znakova. |
| `mapping.const.value` | Vrednost |
| `mapping.const.now` | Današnji datum/vreme |
| `mapping.const.invalid` | Vrednost ne odgovara tipu kolone ({type}). |
| `mapping.loading` | Pripremam predlog mapiranja… |
| `mapping.err.suggest` | Predlog mapiranja nije mogao da se napravi. Možete mapirati kolone ručno. |
| `mapping.empty.noColumns` | U fajlu nema kolona za mapiranje. Vratite se na korak 2. |
| `mapping.empty.noneMapped` | Nijedna kolona nije povezana. Prihvatite predlog ili izaberite ciljne kolone. |
| `rules.hasCondition` | Da li postoji uslov? |
| `rules.add` | Dodaj pravilo |
| `rules.type.conditional` | Uslovna vrednost |
| `rules.type.default` | Podrazumevana vrednost |
| `rules.type.transform` | Transformacija |
| `rules.type.lookup` | Lookup (strani ključ) |
| `rules.type.filter` | Filter redova |
| `rules.transform.trim` | Ukloni razmake |
| `rules.transform.upper` | Velika slova |
| `rules.transform.lower` | Mala slova |
| `rules.transform.dateFormat` | Format datuma |
| `rules.transform.decimal` | Decimalni separator |
| `rules.op.eq` | je jednako |
| `rules.op.neq` | nije jednako |
| `rules.op.gt` | veće od |
| `rules.op.lt` | manje od |
| `rules.op.gte` | veće ili jednako |
| `rules.op.lte` | manje ili jednako |
| `rules.op.contains` | sadrži |
| `rules.op.startsWith` | počinje sa |
| `rules.op.isEmpty` | je prazno |
| `rules.op.isNotEmpty` | nije prazno |
| `rules.lookup.caseNote` | Pretraga ne razlikuje velika i mala slova. |
| `rules.val.value` | Unesite vrednost. |
| `rules.val.column` | Izaberite kolonu. |
| `rules.val.number` | Vrednost mora biti broj. |
| `rules.val.dateFormat` | Format datuma nije ispravan. |
| `rules.confirmDelete` | Obrisati pravilo? |
| `rules.empty` | Za ovu kolonu nema pravila. Pravila nisu obavezna — možete nastaviti. |
| `rules.err.lookupTables` | Lista tabela za lookup nije dostupna. |
| `rules.err.tableMissing` | Tabela „{name}" više ne postoji u izabranoj bazi. Izaberite drugu. |
| `dryrun.title` | Provera pre uvoza |
| `dryrun.noWrite` | Ništa se još ne upisuje u bazu. |
| `dryrun.toImport` | Za uvoz |
| `dryrun.skipped` | Preskočeno |
| `dryrun.errors` | Sa greškom |
| `dryrun.errorsShown` | Greške (prikazano prvih {shown} od {total}) |
| `dryrun.previewTitle` | Pregled prvih 20 redova posle transformacije |
| `dryrun.mode.allOrNothing` | Sve ili ništa — ako ijedan red ima grešku, ništa se ne upisuje. |
| `dryrun.mode.skipInvalid` | Preskoči neispravne — ispravni se upisuju, neispravni se vraćaju u izveštaj. |
| `dryrun.warn.willReject` | Uvoz će biti odbijen jer postoje redovi sa greškom. |
| `dryrun.confirm` | Upisati {n} redova u tabelu {table}? |
| `dryrun.start` | Pokreni uvoz |
| `dryrun.loading` | Proveravam redove… {percent} % |
| `dryrun.err.generic` | Provera nije uspela. |
| `dryrun.empty.nothing` | Nijedan red nije spreman za uvoz. Proverite pravila i mapiranje. |
| `dryrun.empty.noErrors` | Nisu pronađene greške. |
| `import.starting` | Pokrećem uvoz… |
| `import.running` | Uvoz u toku |
| `import.progress` | {done} od {total} redova |
| `import.elapsed` | Proteklo vreme: {time} |
| `import.cancel` | Otkaži uvoz |
| `import.continuesInBackground` | Uvoz se nastavlja i ako zatvorite stranicu. Stanje možete pratiti u istoriji. |
| `import.status.done` | Uvoz je završen |
| `import.status.doneWithErrors` | Uvoz je završen sa greškama |
| `import.status.failed` | Uvoz nije uspeo |
| `import.status.cancelled` | Uvoz je otkazan |
| `import.imported` | Uvezeno |
| `import.skipped` | Preskočeno |
| `import.errors` | Greške |
| `import.duration` | Trajanje |
| `import.err.allOrNothing` | Uvoz nije uspeo. Nijedan red nije upisan. |
| `import.err.partial` | Uvoz je prekinut. Upisano je {n} redova. |
| `import.err.polling` | Status uvoza trenutno nije dostupan. Pokušavam ponovo… |
| `import.err.report` | Izveštaj nije dostupan ili je istekao. |
| `import.empty.zero` | Nijedan red nije upisan. |
| `import.downloadReport` | Preuzmi izveštaj o greškama (.xlsx) |
| `import.showHistory` | Prikaži u istoriji |
| `templates.useTemplate` | Početi iz šablona |
| `templates.none` | Bez šablona |
| `templates.save` | Sačuvaj kao šablon |
| `templates.name` | Naziv |
| `templates.saveHint` | Čuva mapiranje, pravila i konstante. |
| `templates.val.nameRequired` | Naziv je obavezan. |
| `templates.err.duplicate` | Šablon sa tim nazivom već postoji. |
| `templates.saved` | Šablon je sačuvan. |
| `templates.err.invalidColumn` | Šablon se odnosi na kolonu „{name}" koja više ne postoji u bazi. |
| `templates.diff.title` | Šablon „{name}" ne odgovara potpuno fajlu. |
| `templates.diff.new` | Nove kolone u fajlu: {list}. |
| `templates.diff.missing` | Kolone koje fajl nema: {list}. |
| `templates.confirmDelete` | Obrisati šablon „{name}"? Ovo se ne može poništiti. |
| `templates.err.load` | Šabloni nisu mogli da se učitaju. |
| `templates.empty` | Još nema sačuvanih šablona. Šablon možete sačuvati u koraku 4 ili posle uvoza. |
| `history.title` | Istorija uvoza |
| `history.err.load` | Istorija nije mogla da se učita. |
| `history.err.notFound` | Uvoz nije pronađen. |
| `history.empty` | Još nema izvršenih uvoza. |
| `history.emptyFiltered` | Nijedan uvoz ne odgovara filterima. |
| `history.resetFilters` | Poništi filtere |
| `history.errorsTruncated` | Prikazano je prvih 10.000 grešaka. Kompletna lista je u izveštaju. |
| `history.start` | Započni uvoz |
| `error.unexpected` | Došlo je do neočekivane greške. Pokušajte ponovo. |

## 14. Napomene za FE implementaciju
- Teksti iz kataloga (13) su jedini izvor poruka; serverski `ProblemDetails.detail` se prikazuje samo ako je namenjen korisniku, inače se koristi `error.unexpected`.
- Sve brojeve formatirati sa `Intl.NumberFormat('sr-Latn')`, datume `dd.MM.yyyy.`.
- Svaki korak implementirati sa tri vidljiva stanja iz poglavlja 1.3 kao zasebne male komponente (`LoadingState`, `ErrorState`, `EmptyState`) radi ponovne upotrebe.
- Otvorena pitanja (nisu u obimu T02, za PM/CTO): da li korisnik sme da menja detektovani tip kolone; maksimalna veličina fajla (50 MB je pretpostavka); „Sve ili ništa" sa greškama u dry run-u — blokirati ili samo upozoriti (ovde: upozorenje sa potvrdom).
