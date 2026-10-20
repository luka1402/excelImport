SET NOCOUNT ON;

MERGE dbo.Gradovi AS t
USING (VALUES (N'Beograd', N'Srbija'), (N'Novi Sad', N'Srbija'), (N'Niš', N'Srbija'), (N'Zagreb', N'Hrvatska')) AS s(Naziv, Drzava)
ON t.Naziv = s.Naziv
WHEN NOT MATCHED THEN INSERT (Naziv, Drzava) VALUES (s.Naziv, s.Drzava);

MERGE dbo.Kupci AS t
USING (SELECT k.Ime, k.Prezime, k.Email, g.Id AS GradId
       FROM (VALUES (N'Marko', N'Marković', N'marko.markovic@example.com', N'Beograd'),
                    (N'Jelena', N'Jovanović', N'jelena.jovanovic@example.com', N'Novi Sad'),
                    (N'Nikola', N'Nikolić', N'nikola.nikolic@example.com', N'Niš'),
                    (N'Ivana', N'Horvat', N'ivana.horvat@example.com', N'Zagreb')) AS k(Ime, Prezime, Email, Grad)
       JOIN dbo.Gradovi g ON g.Naziv = k.Grad) AS s
ON t.Email = s.Email
WHEN NOT MATCHED THEN INSERT (Ime, Prezime, Email, GradId) VALUES (s.Ime, s.Prezime, s.Email, s.GradId);

MERGE dbo.Proizvodi AS t
USING (VALUES (N'P-001', N'Olovka', CAST(120.00 AS DECIMAL(18,2)), 500),
              (N'P-002', N'Sveska A4', CAST(350.00 AS DECIMAL(18,2)), 200),
              (N'P-003', N'Fascikla', CAST(210.50 AS DECIMAL(18,2)), 150)) AS s(Sifra, Naziv, Cena, Zaliha)
ON t.Sifra = s.Sifra
WHEN NOT MATCHED THEN INSERT (Sifra, Naziv, Cena, Zaliha) VALUES (s.Sifra, s.Naziv, s.Cena, s.Zaliha);
