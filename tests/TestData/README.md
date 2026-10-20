# Test Excel fajlovi (COA-29 / T08)

Ulaz za unit/integracione/E2E testove. Mali fajlovi su u repou (`files/`), veliki se generise skriptom.
Generator nema spoljnih paketa (.xlsx se pise direktno kao OpenXML ZIP) i radi deterministicki.

```
dotnet run tests/TestData/generator/generate.cs               # samples/ + malicious/
dotnet run tests/TestData/generator/generate.cs -- --large    # dodatno large/06-large-100k.xlsx (~5 MB, gitignored)
dotnet run tests/TestData/generator/generate.cs -- --out <dir>
```

Pokrenuti iz korena repoa. Na Windows-u, ako `dotnet run` pukne sa `Value cannot be null (path1)`, proveriti da su
postavljene `LOCALAPPDATA` i `APPDATA`. Fajl `tests/TestData/Directory.Build.props` iskljucuje stroge analizatore za generator.

## samples/ — legitimni i "losi ali bezopasni" fajlovi

| Fajl | Sadrzaj / namena |
|---|---|
| `01-valid.xlsx` | Sheet `Zaposleni`, header u redu 1, 20 redova: Id, Ime, Prezime, Email, Plata (decimal), DatumZaposlenja (datum), Aktivan (bool). Happy path. |
| `02-with-errors.xlsx` | Sheet `Podaci`, 11 redova sa gresakama: los email, tekst u broju/decimali/datumu, duplikat Id i Email, tekst od 500 znakova, negativna i ogromna (1e30) vrednost, los boolean, Id van `int` opsega. Ocekuje se prijava gresaka po redu/koloni. |
| `03-empty-cells.xlsx` | Prazne celije, potpuno prazan red usred podataka, whitespace stringovi (`" "`, tab), prazni redovi na kraju. |
| `04-multi-sheet.xlsx` | Sheet-ovi `Gradovi`, `Kupci`, potpuno prazan `Prazan`, `Proizvodi` i **skriven** `Skriven`. Test izbora sheet-a. |
| `05-header-not-first-row.xlsx` | Naslov i datum u redovima 1–2, prazan red 3, **header u redu 4**, podaci, prazan red i red `Ukupno` na kraju. |
| `07-cyrillic-special.xlsx` | Sheet `Подаци` (cirilica u imenu sheet-a i headerima), dijakritici (đ č ć ž š), emoji, CJK/arapski, zarez/navodnici/`<tag>`/`&`, novi red i tab u celiji, vodeci/zavrsni razmaci, tekst koji lici na formulu (`=1+1`, `@SUM(A1)`), 300 znakova, kontrolni znakovi (izbaceni jer nisu legalni u XML-u). |
| `08-demodb-gradovi.xlsx` | Za `dbo.Gradovi` (Naziv, Drzava): 4 nova grada, validno. |
| `09-demodb-kupci.xlsx` | Za `dbo.Kupci`: Ime, Prezime, Email, Grad (naziv iz seed-a: Beograd/Novi Sad/Niš/Zagreb → FK `GradId`). Validno. |
| `10-demodb-kupci-invalid.xlsx` | Kupci sa kršenjem ogranicenja: nepostojeci grad, email vec u seed-u (`UQ_Kupci_Email`), NULL za Email/Ime (NOT NULL), prezime > 100 znakova. 1 validan red. |
| `11-demodb-proizvodi.xlsx` | Za `dbo.Proizvodi`: validni redovi, `Zaliha` prazna (DEFAULT 0), sifra `P-001` vec u seed-u (`UQ_Proizvodi_Sifra`), cena van `DECIMAL(18,2)`. |

`06-large-100k.xlsx` (100.000 redova + header, 8 kolona, ~5 MB) nastaje samo sa `--large` u `files/large/` i **nije u repou** (.gitignore).

## malicious/ — zlonamerni i neispravni ulazi (sve bi trebalo odbiti ili bezbedno obraditi, bez pada servera)

| Fajl | Napad / ocekivano ponasanje |
|---|---|
| `m01-xlsx-renamed-from-txt.xlsx` | Obican tekst sa `.xlsx` ekstenzijom (nije ZIP) → odbiti, 400. |
| `m02-not-a-zip-png-header.xlsx` | PNG magic bytes sa `.xlsx` ekstenzijom → odbiti (provera sadrzaja, ne samo ekstenzije). |
| `m03-zip-magic-truncated.xlsx` | Pocinje ZIP potpisom (`PK\x03\x04`) ali je odsecen/ostecen → odbiti bez 500. |
| `m04-empty-0-bytes.xlsx` | Fajl od 0 bajtova → odbiti. |
| `m05-script-disguised.xlsx` | `<script>` / PHP tekst kao `.xlsx` → odbiti. |
| `m06-valid-xlsx-wrong-extension.txt`, `m07-...exe` | Ispravan xlsx sa pogresnom ekstenzijom → odbiti po ekstenziji (allow-lista). |
| `m08-zip-bomb.xlsx` | ~100 KB na disku, ~100 MB nezipovan sheet XML (kompresija ~1000:1) → odbiti ili prekinuti pre iscrpljivanja memorije (limit nekompresovane velicine / odnosa kompresije). |
| `m09-sql-injection.xlsx` | SQL injection tekst u celijama **i** u headerima (`'; DROP TABLE Kupci;--`, `]; DELETE ...`, `UNION SELECT`), plus XSS, template (`{{7*7}}`, `${jndi:...}`), path traversal. Nista se ne sme izvrsiti; nazivi kolona se ne smeju konkatenirati u SQL; vrednosti samo parametrizovano; izlaz enkodiran u UI. |
| `m10-xxe-doctype.xlsx` | Sheet XML sa `<!DOCTYPE>` i spoljnim entitetom `file:///C:/Windows/win.ini` → parser mora da odbije DTD; sadrzaj fajla ne sme procureti. |
| `m11-wide-header-1000-columns.xlsx` | 1000 kolona u headeru → razuman limit na broj kolona. |

> Napomena: antivirus alati mogu oznaciti `m08` (zip bomba) i `m06/m07`. Ne otvarati u Excel-u.
> Fajlovi su bezopasni sami po sebi (nema makroa ni izvrsnog koda), ali testiraju odbrambene provere aplikacije.
