# Specificație Completă — Aplicație Procesare XML Facturi (v2.2)

> Înlocuiește `D:\nftosaga\Specificatie_Completa.md` (v1) și versiunile v2 / v2.1. Proiectul se dezvoltă în `D:\nftosaga2026`, **în C# / WPF** (§14); vechiul proiect Python (`D:\nftosaga`) rămâne neatins, ca referință funcțională.
>
> **Stare: toate deciziile de proiectare sunt confirmate.** Documentul este gata pentru implementare. Punctele rămase deschise (două, minore) sunt marcate cu ⚠️ și listate în §0.2.

---

## 0. Rezumatul deciziilor

### 0.1 Confirmate

| Temă | Decizie |
|---|---|
| Preluarea fișierelor sursă | Se **mută** din `D:\Utile\MAGNET\PlusBackOffice\Tmp`, la apăsarea butonului **„Preia fișiere"** (§3) |
| Folder destinație | `D:\De_Importat`, **creat automat** dacă lipsește (§3.4) |
| Coliziune de nume în destinație | Avertizare + **sufix incremental** (§3.4) |
| Raport XLSX | **Opțional**, prin checkbox (§9.1); include coloana `Descriere` (§10.2) |
| Anti-duplicare Intrări | `InAnte.txt`, cheie = `<FacturaInformatiiSuplimentare>` (§4.1) |
| Anti-duplicare Ieșiri | `IeAnte.txt`, **același folder** cu `InAnte.txt`; cheie = `<FacturaNumar>` **prefixat** + `<FacturaData>` (§4.2) |
| Momentul scrierii în registru | La generarea cu succes a fișierului, cu anulare de lot (§4.3–4.4) |
| Agregarea liniilor pe cotă TVA | **Doar la Intrări** (§8) |
| `Cantitate` pe linia de Intrări | **`1`** sau **`-1`**, după semnul `<Valoare>` (§8.3) |
| `Pret` pe linia de Intrări | **`abs(Valoare)`** — preț unitar convențional (§8.3.1) |
| `PretVanzare` | Se preia cel **nenul**, **nemodificat ca semn** (§8.3) |
| `LinieNrCrt` | **Renumerotat** consecutiv după agregare (§8.3) |
| `<ClientNume>` la Intrări | Înlocuit cu **`ANCAFARM`**; gestiunea e dată de `<Activitate>` (§7.3) |
| Conversia datelor în XML | **Nu mai este necesară** — sursa vine în `dd.MM.yyyy` (§7.3) |
| `coresp.xlsx` | **Eliminat**; `COD` se preia din `<Antet><Cod>` (§10.3) |
| Structura DBF | Documentată integral (§10.3); neschimbată față de cea existentă |
| **Limbaj și platformă** | **C# / WPF pe .NET Framework 4.8** — nu Python, din motive de compatibilitate cu Norton 360 (§14.1) |
| Diacritice în DBF | **Transliterare fără diacritice** (§10.3.1) |

### 0.2 ⚠️ Rămase deschise (minore)

1. **Numărul de zecimale la scrierea XML-ului de ieșire.** Sursa are `<TVA>` cu 6 zecimale (`17.940000`) și `<Valoare>`/`<Pret>`/`<PretVanzare>` cu 2. Provizoriu: se **păstrează numărul de zecimale din sursă, per tag**. *(Distinct de formatul DBF, care e fixat prin structura câmpurilor — §10.3.)*
2. **Sheet-ul „Jurnal" în raportul XLSX** (§10.2) — facturi excluse ca duplicat, nume negăsite în `Grupe.xlsx`, avertizări de agregare. Util sau zgomot?

---

## 1. Context și schimbări față de v1

Fișierele XML sursă vin dintr-un **export nou din ERP**, cu diferențe majore față de formatul vechi:

| Aspect | V1 (vechi) | V2.2 (actual) |
|---|---|---|
| Nume fișier sursă | `FIN_*.xml` / `FIE_*.xml` (tipul rezultă din prefix) | `F_13150581_ddmmyyyy_HHMMSS.xml` — **numele nu mai indică tipul** |
| Locație fișiere sursă | selectată manual | **`D:\Utile\MAGNET\PlusBackOffice\Tmp`**, preluare prin mutare (§3) |
| Locație fișiere generate | selectată manual | **`D:\De_Importat`** (§3.4) |
| Determinare tip (Intrări/Ieșiri) | din prefixul numelui | din **`<FurnizorCIF>`** (§2) |
| `<Cont>` / `<Activitate>` pe `<Linie>` | absente în sursă, completate din `Grupe.xlsx` | **completate de ERP**, inclusiv coduri de excepție (`7588`, `709`) — **nu se mai suprascriu** (§5) |
| `<FacturaData>` / `<FacturaScadenta>` | `yyyy-MM-dd`, convertite de aplicație | deja `dd.MM.yyyy` — **fără conversie** pentru XML (§7.3) |
| Linii pe aceeași cotă TVA | o singură linie | **2+ linii la Intrări** → se agregă (§8) |
| Raport XLSX | generat întotdeauna | **opțional** (§9.1) |
| Evitarea reimportului | doar Intrări, `InAnte.txt` întreținut manual | **Intrări + Ieșiri**, registre întreținute automat (§4) |
| `coresp.xlsx` | necesar pentru câmpul `COD` din DBF | **eliminat** (§10.3) |

Un fișier XML exportat din ERP conține, momentan, **un singur tip** de facturi. Separarea automată a unui fișier „amestecat" este o dezvoltare viitoare, **în afara scopului curent**.

### 1.1 De ce se generează în continuare DBF

SAGA **nu importă `nr_nir` din XML** — este o restricție a programului. Numărul NIR ajunge în contabilitate exclusiv prin fișierul DBF, câmpul `NR_NIR`. Acesta este motivul pentru care Intrările necesită și azi generare DBF, pe lângă XML.

---

## 2. Determinarea tipului (Intrări vs. Ieșiri)

- Tipul **nu** se determină din numele fișierului.
- Se citește `<FurnizorCIF>` din `<Antet>`:
  - `<FurnizorCIF>` == `13150581` (CIF ANCAFARM) → **Ieșiri** (ANCAFARM e furnizorul).
  - Altfel → **Intrări** (ANCAFARM e clientul).
- Se verifică **toate** facturile din fișier:
  - toate de același tip → se procedează normal;
  - fișier amestecat → **eroare**, procesarea nu începe.

---

## 3. Flux de fișiere: preluare din sursă și livrare în destinație

### 3.1 Foldere implicite

| Rol | Cale implicită | Configurabil |
|---|---|---|
| Folder sursă (export ERP) | `D:\Utile\MAGNET\PlusBackOffice\Tmp` | da |
| Folder destinație | `D:\De_Importat` | da |
| Arhivă surse procesate | `D:\nftosaga2026\Arhiva\<AAAA-LL>\` | da |
| Registre (`InAnte.txt`, `IeAnte.txt`) | `D:\nftosaga2026\` | da |

Căile se rețin într-un fișier de configurare (`config.json`) lângă executabil.

### 3.2 Preluarea fișierelor sursă

1. Aplicația scanează folderul sursă după `F_13150581_*.xml`.
2. Afișează lista în GUI: nume, data/ora din nume, dimensiune, număr de facturi, tip detectat.
3. Utilizatorul selectează fișierele și apasă **„Preia fișiere"**.
4. Fișierele selectate se **mută** din `Tmp` în folderul de lucru al aplicației.

> Mutarea (nu copierea) este intenționată: `Tmp` rămâne curat, iar un fișier deja preluat nu mai poate fi procesat din greșeală a doua oară din sursă.

### 3.3 Arhivarea surselor procesate

După procesare, fișierul sursă se mută în `Arhiva\<AAAA-LL>\`, cu numele original. Deoarece preluarea este o mutare, arhiva devine **singura copie** a exportului ERP — motiv pentru care arhivarea este obligatorie, nu opțională, și se face înainte de ștergerea oricărui fișier de lucru.

Dacă procesarea **eșuează**, fișierul rămâne în folderul de lucru (nearhivat) și e semnalat în GUI ca „nefinalizat", ca să poată fi reluat sau mutat manual înapoi.

### 3.4 Livrarea în destinație

- Toate fișierele generate (XML, DBF, XLSX opțional) se scriu în `D:\De_Importat`, conform §11.
- Dacă folderul nu există, se **creează automat**.
- Dacă un fișier cu același nume există deja: **avertizare** în jurnal + salvare cu **sufix incremental** (`..._IN_2.xml`, `..._IN_3.xml`).

---

## 4. Evidența facturilor deja importate (anti-duplicare)

O factură transmisă o dată către SAGA nu trebuie să ajungă a doua oară într-un fișier de import, chiar dacă exportul ERP o include din nou.

### 4.1 Intrări — `InAnte.txt`

- Conține **numerele de identificare ale intrărilor deja importate**, respectiv conținutul `<FacturaInformatiiSuplimentare>`, **o valoare pe linie**.
- Pentru fiecare `<Factura>`:
  - valoare regăsită în `InAnte.txt` → factura este **exclusă** din XML, din DBF și din raport; notă în jurnal;
  - altfel → se procesează normal, iar valoarea intră în lista de adăugat.
- Comparație pe valoare **normalizată** (trim la capete, insensibil la majuscule).
- Factură fără `<FacturaInformatiiSuplimentare>` sau cu valoare goală → **avertizare**; factura **rămâne** în output (nu poate fi verificată, deci nu poate fi declarată duplicat) și **nu** se adaugă nimic în registru.

### 4.2 Ieșiri — `IeAnte.txt`

Se stochează în **același folder** cu `InAnte.txt` (§3.1). Cheia de unicitate este perechea `<FacturaNumar>` **prefixat** + `<FacturaData>`. Plajele de numere nu se reinițializează anual, deci anul nu e necesar separat; data e inclusă ca dublă siguranță.

Format — o linie per factură, componente separate prin `|`:

```
BTANCA2 1611280219|21.07.2026
```

- Pereche regăsită în registru → factura **exclusă** din XML și din raport, cu notă în jurnal.
- Perechile facturilor efectiv generate se adaugă la registru (§4.3).

### 4.3 Automatizarea întreținerii registrelor

La deschiderea oricărui registru, aplicația:

1. **Citește** toate liniile, ignorând liniile goale și cele care încep cu `#`.
2. **Verifică unicitatea**: elimină duplicatele, păstrând prima apariție; notează în jurnal câte au fost eliminate.
3. **Face copie de siguranță** înainte de orice scriere: `InAnte.txt.bak` / `IeAnte.txt.bak` (o singură generație, suprascrisă).
4. La finalul unei procesări reușite, **adaugă** valorile noi, în ordinea apariției în XML, precedate de o linie-comentariu de lot:
   ```
   # lot 2026-07-23 14:32 — F_13150581_23072026_143012.xml (14 facturi)
   42927
   42931
   ```
5. **Scriere atomică**: fișier temporar + înlocuire, ca o întrerupere să nu corupă registrul.
6. Encoding **UTF-8**, terminații de linie `CRLF`.

### 4.4 Anularea ultimului lot

Buton în GUI care elimină din registru liniile adăugate la ultima procesare (identificate prin linia-comentariu de lot). Necesar dacă importul în SAGA eșuează după generarea fișierelor.

### 4.5 Vizualizare din GUI

Tab separat cu conținutul ambelor registre (listă, căutare, număr total), buton „Deschide în editor extern". Editarea manuală rămâne posibilă; deduplicarea de la §4.3 pct. 2 curăță greșelile la următoarea rulare.

---

## 5. `<Cont>` și `<Activitate>` — nu se suprascriu

XML-ul sursă vine cu `<Cont>` și `<Activitate>` completate pe fiecare `<Linie>`, inclusiv coduri de excepție (`Cont=7588` pentru vânzări către persoane fizice, `Cont=709` pentru reduceri comerciale) care nu apar în `Grupe.xlsx` și nu trebuie modificate.

- Aplicația **nu completează și nu suprascrie** `<Cont>` / `<Activitate>` din `Grupe.xlsx`.
- `Grupe.xlsx` se folosește doar pentru **validare**: se caută `ClientNume` (Intrări) / `FurnizorNume` (Ieșiri) în sheet-ul corespunzător.
  - nume negăsit → avertizare („nume necunoscut, posibil client/depozit nou"), dar **factura rămâne în output**;
  - nume găsit → nicio acțiune suplimentară.
- GUI-ul păstrează tabelul editabil cu maparea, ca referință vizuală.

---

## 6. Procesare XML — Ieșiri (`FurnizorCIF = 13150581`)

Ordinea operațiilor:

1. Detectare tip (§2) și validare `Grupe.xlsx` (§5).
2. Calculul numărului prefixat (§6.3) și **excluderea facturilor deja importate** din `IeAnte.txt` (§4.2).
3. Redenumire `<CodArticolClient>` → `<Descriere>` (§6.1).
4. Remapare `<Activitate>` (§6.2).
5. Aplicarea prefixului la `<FacturaNumar>` (§6.3).
6. Generare fișiere (§9) și actualizare `IeAnte.txt` (§4.3).

**Agregarea liniilor (§8) nu se aplică la Ieșiri.** Liniile rămân exact ca în sursă.

Nu se face conversia datelor și nu se înlocuiește `FurnizorNume` cu `ANCAFARM`.

### 6.1 Redenumire tag: `<CodArticolClient>` → `<Descriere>`

În fiecare `<Linie>` din `<Detalii><Continut>`, `<CodArticolClient>` este **redenumit** `<Descriere>`, cu conținutul neschimbat. `<CodArticolClient>` e opțional și adesea gol; `<Descriere>` crește calitatea informației importate.

### 6.2 Remapare `<Activitate>`

| Sursă | Nouă |
|---|---|
| `01` | `11` |
| `02` | `12` |
| `03` | `13` |
| `04` | `14` |

> Toate liniile aceleiași facturi au aceeași `Activitate`. Dacă apare o factură cu activități diferite pe linii → avertizare în jurnal, prefixul se ia din prima linie.

### 6.3 Prefixare `<FacturaNumar>`

| Activitate (01/11 … 04/14) | Prefix |
|---|---|
| 01 / 11 | `BTANCA1 ` |
| 02 / 12 | `BTANCA2 ` |
| 03 / 13 | `BTANCA3 ` |
| 04 / 14 | `BTANCA4 ` |

Exemplu: `<FacturaNumar>7996</FacturaNumar>` cu Activitate 01 → `<FacturaNumar>BTANCA1 7996</FacturaNumar>`.

### 6.4 Fișiere generate

- XML modificat — **întotdeauna**.
- Raport XLSX (§10.2) — **opțional**.
- **Fără DBF.**

---

## 7. Procesare XML — Intrări (`FurnizorCIF ≠ 13150581`)

Ordinea operațiilor:

1. Detectare tip (§2) și validare `Grupe.xlsx` (§5).
2. **Excluderea facturilor deja importate** din `InAnte.txt` (§4.1).
3. **Agregarea liniilor pe cotă TVA** (§8).
4. Redenumire `<CodArticolFurnizor>` → `<Descriere>` (§7.1).
5. Înlocuire `<ClientNume>` → `ANCAFARM` (§7.3).
6. Generare fișiere: XML, **DBF** (§10.3), XLSX opțional (§9); actualizare `InAnte.txt` (§4.3).

### 7.1 Redenumire tag: `<CodArticolFurnizor>` → `<Descriere>`

Analog cu §6.1: în fiecare `<Linie>`, `<CodArticolFurnizor>` devine `<Descriere>`, conținut neschimbat.

> Ordinea contează: agregarea (§8) folosește conținutul acestui tag ca parte de cheie, deci se execută **înainte** de redenumire.

### 7.2 Excluderea prin `InAnte.txt`

Vezi §4.1 și §4.3. Față de v1, fișierul nu mai trebuie întreținut manual: aplicația îl citește, îl deduplică și îl completează automat. Dacă lipsește la prima rulare, se creează **gol**, cu avertizare.

### 7.3 Reguli specifice Intrări

- `<ClientNume>` → înlocuit cu **`ANCAFARM`** în toate `<Antet>`-urile. În export valoarea poate fi `ANCAFARM2` sau altă variantă; se normalizează, deoarece **diferența între gestiuni este dată de `<Activitate>`** (care ajunge în câmpul `GRUPA` din DBF), nu de numele clientului.
- **Fără conversie de dată în XML** — sursa vine în `dd.MM.yyyy`, formatul așteptat. *(Conversia rămâne obligatorie doar pentru DBF, unde `DATA`/`SCADENT` sunt câmpuri de tip dată — §10.3.)*
- Generare DBF (§10.3), fără `coresp.xlsx`.

### 7.4 `<Cont>` / `<Activitate>`

Ca la Ieșiri (§5): nu se suprascriu; `Grupe.xlsx` doar validează.

---

## 8. Agregarea liniilor pe cotă de TVA — **doar Intrări**

### 8.1 Problema

Noul export ERP poate produce **două sau mai multe `<Linie>` pentru aceeași cotă de TVA** în cadrul aceleiași facturi (tipic: o linie de valoare și o linie de rotunjire). Importul în SAGA trebuie să primească o singură linie consolidată.

Regula se aplică **exclusiv facturilor de Intrări**. Ieșirile păstrează liniile ca în sursă (§6).

### 8.2 Cheia de grupare

Două linii ale aceleiași facturi se agregă dacă au **toate** valorile identice:

1. **descrierea** — `<CodArticolFurnizor>` (ex. `Intrare marfa 21%`); unde exportul folosește `<Explicatii>` ca purtător al descrierii, se citește acela;
2. `<Cont>` (ex. `371.00002`);
3. `<ProcTVA>` (ex. `21.00`).

Comparație pe valori normalizate (trim; în rest, comparație exactă de șiruri). Gruparea se face **în interiorul unei singure facturi**, niciodată între facturi.

### 8.3 Regula de consolidare

Structura completă a unei `<Linie>` și tratamentul fiecărui tag:

| Tag | Regulă |
|---|---|
| `<LinieNrCrt>` | **renumerotare** consecutivă 1, 2, 3… după agregare |
| `<Gestiune>` | din prima linie a grupului (gol în exportul actual) |
| `<Activitate>` | din prima linie a grupului |
| `<CodArticolFurnizor>` | identic prin definiția cheii; redenumit ulterior `<Descriere>` (§7.1) |
| `<GUID_cod_articol>` | din prima linie a grupului |
| `<UM>` | din prima linie a grupului |
| `<Cantitate>` | **`1`** dacă `Valoare >= 0`, **`-1`** dacă `Valoare < 0` |
| `<Pret>` | **`abs(Valoare)`** |
| `<Valoare>` | **suma** valorilor din grup |
| `<ProcTVA>` | identic prin definiția cheii |
| `<TVA>` | **suma** valorilor din grup |
| `<Cont>` | identic prin definiția cheii |
| `<PretVanzare>` | valoarea **nenulă** din grup, **cu semnul din sursă** (rămâne pozitivă și la storno) |

Reguli de calcul:

- Sumele se calculează cu `decimal.Decimal`, **niciodată `float`**.
- La scrierea XML-ului se păstrează numărul de zecimale din sursă, per tag (⚠️ §0.2 pct. 1).
- Valorile pot fi **negative** (storno, rotunjiri); nu se filtrează pe semn.

#### 8.3.1 De ce `Cantitate = ±1` și `Pret = abs(Valoare)`

Prețul unitar din export este o convenție, nu o informație contabilă folosită la import — `<Valoare>` este cea care contează. Fixând `Cantitate` la semnul valorii și `Pret` la valoarea absolută, relația `Cantitate × Pret = Valoare` rămâne exactă în ambele situații, deci nu apar diferențe de rotunjire pe care SAGA le-ar putea semnala:

- factură normală: `1 × 85.40 = 85.40` ✔
- factură storno: `-1 × 855.10 = -855.10` ✔

`<PretVanzare>` **nu** urmează semnul operațiunii: prețul de vânzare este atribut al articolului, nu al operațiunii, și rămâne pozitiv inclusiv pe storno (confirmat de DBF-ul de referință, §10.3.2).

Regula `Cantitate = ±1` se aplică **tuturor liniilor de Intrări**, inclusiv celor care nu au fost consolidate (grup de o singură linie).

### 8.4 Exemplu — factura DR.MAX 1611280219 / 21.07.2026

Sursă — două linii cu aceeași cheie (`Intrare marfa 21%` + `371.00002` + `21.00`):

| LinieNrCrt | Cantitate | Pret | Valoare | TVA | PretVanzare |
|---|---|---|---|---|---|
| 1 | -1 | 0.01 | -0.01 | -0.010000 | 0.00 |
| 2 | 1 | 85.41 | 85.41 | 17.940000 | 162.95 |

Rezultat — o singură linie:

| LinieNrCrt | Cantitate | Pret | Valoare | TVA | PretVanzare |
|---|---|---|---|---|---|
| 1 | 1 | 85.40 | 85.40 | 17.930000 | 162.95 |

`Valoare` și `TVA` sunt sume; `Cantitate` = `1` (valoare pozitivă); `Pret` = `abs(Valoare)` = `85.40`, **nu** `85.41` de pe linia 2; `PretVanzare` de pe linia 2, singura nenulă. Linia de rotunjire de `-0.01` este absorbită complet.

### 8.5 Cazuri limită

| Situație | Comportament |
|---|---|
| Grup cu o singură linie | Se aplică totuși `Cantitate = ±1` și `Pret = abs(Valoare)` (§8.3.1) |
| Grup cu 3+ linii | Aceeași regulă; `Valoare`/`TVA` se sumează peste toate |
| **Nicio** linie din grup cu `PretVanzare != 0` | `PretVanzare = 0`; avertizare în jurnal |
| **Mai multe** linii cu `PretVanzare != 0` și valori diferite | Avertizare cu factura și valorile în conflict; se preia prima; factura e marcată pentru verificare manuală |
| `<Activitate>` diferită în același grup | Avertizare; se preia din prima linie |
| Total factură după agregare ≠ total înainte | **Eroare** — se raportează factura și diferența |

### 8.6 Control de consistență

După agregare, pentru fiecare factură: `Σ Valoare` și `Σ TVA` peste liniile consolidate trebuie să fie egale cu sumele peste liniile originale (toleranță 0,01 lei). Neconcordanțele se raportează explicit — este singura garanție că regula nu pierde bani.

---

## 9. Fișiere generate — rezumat pe tip

| Fișier | Intrări | Ieșiri |
|---|---|---|
| XML modificat | ✅ întotdeauna | ✅ întotdeauna |
| Raport XLSX | ⚙️ opțional | ⚙️ opțional |
| DBF (`IN_..._AF.DBF`) | ✅ întotdeauna | ❌ |

### 9.1 Caracterul opțional al raportului XLSX

- Checkbox **„Generează raport XLSX"** în GUI, cu starea salvată în configurare între rulări.
- Valoare implicită: **bifat**.
- Debifat → nu se creează `..._raport.xlsx`; procesarea e sensibil mai rapidă pe fișiere mari.
- Debifarea **nu** afectează XML-ul, DBF-ul sau registrele (§4).

---

## 10. Structura fișierelor generate

### 10.1 XML

Structura sursă, cu modificările din §6 (Ieșiri) sau §7 (Intrări) și, pentru Intrări, cu liniile consolidate conform §8. Encoding și declarație XML identice cu ale sursei.

### 10.2 Raport XLSX

Coloanele rămân cele din v1, cu **includerea coloanei `Descriere`** (provenită din `CodArticolFurnizor` / `CodArticolClient` — §6.1 / §7.1).

*(Restul coloanelor — FurnizorNume, FurnizorCIF, FacturaNumar, FacturaData, FacturaScadenta, Cod, LinieNrCrt, Activitate, Cantitate, Pret, Valoare, ProcTVA, TVA, Cont, PretVanzare pentru Intrări; ClientNume, ClientCIF, ClientJudet, ClientTara, ClientLocalitate + restul pentru Ieșiri — identic cu v1.)*

- Pentru Ieșiri, `FacturaNumar` reflectă deja prefixul `BTANCAx ` (§6.3).
- Raportul conține **doar facturile incluse efectiv în output** (cele excluse prin §4 nu apar).
- ⚠️ §0.2 pct. 2: sheet suplimentar „Jurnal" cu excluderile și avertizările — de decis.

### 10.3 DBF (doar Intrări)

Structura este cea existentă, documentată aici integral pe baza fișierului de referință `IN_22-07-2026_23-07-2026_AF.dbf`. **Nu se modifică nimic** — nici nume de câmpuri, nici tipuri, nici lungimi, nici ordine.

Antet: dBase III (`0x03`), lungime înregistrare **310** octeți (1 marcaj ștergere + 309 date), **20 câmpuri**, fără code page setat (language driver `0x00`).

| # | Câmp | Tip | Lung. | Zec. | Sursă |
|---|---|---|---|---|---|
| 1 | `NR_NIR` | C | 16 | — | **`<FacturaInformatiiSuplimentare>`** |
| 2 | `NR_INTRARE` | C | 16 | — | **`<FacturaNumar>`** |
| 3 | `GESTIUNE` | C | 4 | — | **gol** |
| 4 | `DEN_GEST` | C | 36 | — | **gol** |
| 5 | `COD` | C | 5 | — | `<Antet><Cod>` (ex. `01853`) |
| 6 | `DATA` | D | 8 | — | `<FacturaData>`, conv. `dd.MM.yyyy` → `YYYYMMDD` |
| 7 | `SCADENT` | D | 8 | — | `<FacturaScadenta>`, conv. `dd.MM.yyyy` → `YYYYMMDD` |
| 8 | `TIP` | C | 1 | — | **gol** |
| 9 | `TVAI` | N | 1 | 0 | `<FacturaTVAIncasare>`: `Nu` → `0`, `Da` → `1` |
| 10 | `COD_ART` | C | 16 | — | **gol** |
| 11 | `DEN_ART` | C | 60 | — | `<CodArticolFurnizor>` / `<Descriere>` |
| 12 | `UM` | C | 5 | — | `<UM>` (ex. `BUC`) |
| 13 | `CANTITATE` | N | 14 | 3 | `1.000` sau `-1.000`, după semnul `<Valoare>` (§8.3) |
| 14 | `DEN_TIP` | C | 36 | — | **gol** |
| 15 | `TVA_ART` | N | 2 | 0 | `<ProcTVA>` ca întreg (`11.00` → `11`, `21.00` → `21`) |
| 16 | `VALOARE` | N | 15 | 2 | `<Valoare>` agregată (§8) |
| 17 | `TVA` | N | 15 | 2 | `<TVA>` agregat (§8) |
| 18 | `CONT` | C | 20 | — | `<Cont>` (ex. `371.00001`) |
| 19 | `PRET_VANZ` | N | 15 | 2 | `<PretVanzare>`, cu semnul din sursă |
| 20 | `GRUPA` | C | 16 | — | `<Activitate>` — **discriminantul de gestiune** |

> Câmpurile marcate **gol** rămân goale prin proiectare: importul deservește **contabilitatea financiară**, nu cea de gestiune, deci nu are nevoie de ele.

#### 10.3.1 Reguli de scriere

- O înregistrare per **linie consolidată** (§8), nu per linie din XML.
- Câmpurile `C` se completează cu spații la dreapta; `N` se aliniază la dreapta, cu punct zecimal.
- Datele (`D`) se scriu `YYYYMMDD` — conversia din `dd.MM.yyyy` este **obligatorie** aici, chiar dacă nu se mai face în XML (§7.3).
- **Diacriticele se elimină prin transliterare** înainte de scriere: `ă`/`â` → `a`, `î` → `i`, `ș`/`ş` → `s`, `ț`/`ţ` → `t`, plus majusculele corespunzătoare. Se acoperă astfel și variantele cu sedilă din exportul ERP (ex. `Mogoşoaia`). Fișierul nu are code page setat, deci orice octet peste 127 este ambiguu la citire.
- Textele care depășesc lungimea câmpului se **trunchiază**, cu avertizare în jurnal (relevant mai ales pentru `DEN_ART`, 60 caractere).
- `TVA_ART` este întreg pe 2 poziții — o cotă de TVA cu zecimale nenule nu încape; dacă apare → **eroare**, cu factura raportată.

#### 10.3.2 Referință — factură normală și storno

Cele două înregistrări din fișierul de referință:

| Câmp | Înreg. 1 (normală) | Înreg. 2 (**storno**) |
|---|---|---|
| `NR_NIR` | `49812` | `49809` |
| `NR_INTRARE` | `1780071` | `3845859` |
| `COD` | `00081` | `01210` |
| `DATA` / `SCADENT` | `20260722` / `20261109` | `20260722` / `20261104` |
| `DEN_ART` | `Medicamente cu TVA 11%` | `Medicamente cu TVA 11%` |
| `UM` | `BUC` | `BUC` |
| `CANTITATE` | `1.000` | **`-1.000`** |
| `TVA_ART` | `11` | `11` |
| `VALOARE` | `665.80` | **`-855.10`** |
| `TVA` | `73.24` | **`-94.06`** |
| `CONT` | `371.00001` | `371.00001` |
| `PRET_VANZ` | `857.30` | `1104.95` (**pozitiv**) |
| `GRUPA` | `01` | `01` |

Acest fișier este **testul de referință** al implementării: regenerat din XML-urile sursă corespunzătoare, rezultatul trebuie să fie identic octet cu octet (cu excepția datei din antetul DBF).

DBF-ul reflectă liniile **după agregare** (§8) și **fără** facturile excluse prin `InAnte.txt` (§4.1).

---

## 11. Numele fișierelor rezultate

Convenția din v1, cu scriere în `D:\De_Importat`:

- Intrări: `F_13150581_ddmmyyyy_DDMMYYYY_IN.xml`, raport `..._IN_raport.xlsx`, DBF `IN_ddmmyyyy_DDMMYYYY_AF.DBF`
- Ieșiri: `F_13150581_ddmmyyyy_DDMMYYYY_IE.xml`, raport `..._IE_raport.xlsx`
- `ddmmyyyy_DDMMYYYY` = data minimă / maximă din `<FacturaData>` ale facturilor **rămase după excludere** (§4).

> Riscul de suprascriere semnalat în v2 dispare: sursa este `…\Tmp`, destinația este `D:\De_Importat`.

---

## 12. Tratarea erorilor

| Situație | Comportament |
|---|---|
| `<FurnizorCIF>` lipsă sau fișier cu tipuri amestecate | Eroare, procesarea nu începe (§2) |
| Nume din XML negăsit în `Grupe.xlsx` | Avertizare; **factura rămâne** în output |
| `Grupe.xlsx` lipsește sau nu poate fi citit | Eroare, procesarea nu începe |
| XML-ul nu conține elemente `<Factura>` | Avertizare, nu se generează output |
| Folder sursă inexistent / inaccesibil | Eroare la scanare, cu mesaj explicit; aplicația pornește totuși |
| Mutarea din `Tmp` eșuează (fișier blocat de alt proces) | Eroare pe fișierul respectiv, restul lotului continuă |
| Folder destinație inexistent | Se creează automat; dacă nu se poate crea → eroare |
| Fișier cu același nume în destinație | Avertizare + sufix incremental (§3.4) |
| `InAnte.txt` / `IeAnte.txt` inexistent | Se creează gol, cu avertizare |
| Registru cu duplicate | Deduplicare automată + notă în jurnal (§4.3) |
| Toate facturile din fișier deja importate | Avertizare, **nu se generează output**, registrele rămân neschimbate |
| Factură fără `<FacturaInformatiiSuplimentare>` (Intrări) | Avertizare; rămâne în output, nu se adaugă în registru (§4.1) |
| `<Antet><Cod>` lipsă sau gol (Intrări) | Avertizare; `COD` rămâne gol, factura rămâne în output |
| `<ProcTVA>` cu zecimale nenule (nu încape în `TVA_ART`) | Eroare pe factură, raportată explicit (§10.3.1) |
| `DEN_ART` peste 60 caractere | Trunchiere + avertizare (§10.3.1) |
| Agregare: totaluri diferite înainte/după | Eroare pe factură, raportată explicit (§8.6) |
| Eroare la scrierea registrului | Eroare; restaurare din `.bak`; fișierele generate rămân, se semnalează că registrul **nu** a fost actualizat |
| Procesare eșuată după mutarea din `Tmp` | Fișierul rămâne în folderul de lucru, marcat „nefinalizat" (§3.3) |

---

## 13. Interfață grafică

- Eticheta de tip operație afișează rezultatul detectat din `<FurnizorCIF>`, nu din numele fișierului.
- Panou **„Fișiere sursă"**: listă din `D:\Utile\MAGNET\PlusBackOffice\Tmp` cu tip detectat și număr de facturi; butoane „Reîmprospătează" și **„Preia fișiere"** (§3.2).
- Câmpuri pentru folderul sursă și cel destinație, precompletate din §3.1 și salvate în configurare.
- Checkbox **„Generează raport XLSX"** (§9.1).
- Tab **„Registre"**: conținutul `InAnte.txt` și `IeAnte.txt`, cu căutare, număr total, buton „Anulează ultimul lot" (§4.4) și „Deschide în editor extern".
- Tabelul de mapare din `Grupe.xlsx` rămâne editabil, cu rol strict de **validare** (§5).
- **Eliminat:** câmpul de selecție `coresp.xlsx` (§10.3).
- Jurnalul de procesare are secțiuni distincte pentru facturile excluse ca duplicat și pentru avertizările de agregare, cu numărătoare (ex. „3 facturi excluse, 12 linii consolidate în 6").

---

## 14. Implementare tehnică

### 14.1 Limbaj și platformă

Aplicația se dezvoltă în **C# / WPF**, nu în Python.

Motivul este compatibilitatea cu **Norton 360**, încă în uz la client. Experiența cu `anaf_collector.exe` a arătat că problema nu ține de codul aplicației, ci de modul de împachetare: un executabil PyInstaller conține un bootloader care, la fiecare pornire, extrage interpretorul și bibliotecile într-un director temporar și execută cod de acolo — exact tiparul comportamental pe care stratul **SONAR** îl clasifică drept suspect. Excluderile de scanare nu acoperă SONAR, deci nu constituie o soluție stabilă. Un executabil .NET gestionat nu despachetează nimic la rulare și nu declanșează această euristică.

| Aspect | Alegere | Motiv |
|---|---|---|
| Limbaj | **C#** | Cod gestionat, fără bootloader de despachetare |
| UI | **WPF** (XAML) | Același model ca la celelalte utilitare din mediu; `DataGrid` acoperă tabelul `Grupe.xlsx` și lista de fișiere sursă |
| Framework | **.NET Framework 4.8** | Preinstalat pe Windows 10 (1903+) și 11 — **zero runtime de distribuit**, executabil mic, cu profil de risc minim la scanare |
| Tip build | `Release`, **x86 sau AnyCPU**, executabil clasic + DLL-uri alături | Vezi §14.4 |

> Alternativa .NET 8 este viabilă tehnic, dar cere fie instalarea runtime-ului pe fiecare stație, fie publicare **self-contained single-file** — iar aceasta din urmă reintroduce exact comportamentul de extragere în `%TEMP%` care a cauzat problema la `anaf_collector.exe`. Dacă se trece totuși la .NET 8, se folosește publicare *framework-dependent*, fără `PublishSingleFile`.

### 14.2 Dependențe

| Nevoie | Soluție | Observații |
|---|---|---|
| XML | `System.Xml.Linq` (`XDocument`) | În framework, fără pachet extern |
| Calcule monetare | tipul nativ **`decimal`** | Zecimal pe 128 biți — corespondentul lui `decimal.Decimal` din Python; **niciodată `double`** |
| Citire `Grupe.xlsx` / scriere raport | **ClosedXML** (NuGet, licență MIT) | Sau `DocumentFormat.OpenXml` direct, dacă se preferă mai puține dependențe |
| Scriere DBF | **cod propriu**, `BinaryWriter` | Vezi §14.3 |
| Configurare | JSON simplu (`Newtonsoft.Json`) sau `Settings` din proiect | Alegere liberă; fără impact funcțional |

Regula generală: **cât mai puține pachete NuGet**. Fiecare DLL suplimentar crește suprafața de analiză a antivirusului și nu aduce nimic aici.

### 14.3 Scrierea DBF-ului

Nu se folosește niciun driver ODBC/OLE DB pentru dBASE sau Visual FoxPro: acestea sunt pe 32 de biți, cer instalare separată pe fiecare stație și nu oferă control asupra octeților scriși.

Fișierul se scrie **direct, cu `BinaryWriter`**, după structura complet documentată în §10.3:

- antet de 32 octeți (versiune `0x03`, data ultimei modificări, număr înregistrări, lungime antet, lungime înregistrare);
- 20 de descriptori de câmp a câte 32 de octeți;
- terminator de antet `0x0D`;
- înregistrări de 310 octeți (1 marcaj de ștergere ` ` + 309 octeți de date, câmpurile completate cu spații la dreapta pentru `C`, aliniate la dreapta pentru `N`);
- terminator de fișier `0x1A`.

Sunt aproximativ 100 de linii de cod, fără nicio dependență, și garantează potrivirea octet cu octet cu fișierul de referință (§10.3.2). Textul se scrie ca **ASCII**, după transliterarea diacriticelor (§10.3.1).

Datele (`DATA`, `SCADENT`) se obțin cu `DateTime.ParseExact(v, "dd.MM.yyyy", CultureInfo.InvariantCulture)` și se scriu `ToString("yyyyMMdd")`. **Toate** conversiile numerice și de dată folosesc `CultureInfo.InvariantCulture` — pe o stație cu regional settings românești, separatorul zecimal implicit este virgula, ceea ce ar corupe tăcut atât DBF-ul cât și XML-ul.

### 14.4 Împachetare și reputație la antivirus

Alegerea C# reduce mult riscul, dar nu îl elimină: SONAR penalizează și **fișierele nesemnate cu prevalență scăzută**, iar fiecare recompilare produce un hash nou, deci o reputație de la zero.

Măsuri, în ordinea eficienței:

1. **Semnare Authenticode** — soluția durabilă. Reputația se atașează certificatului, nu hash-ului, deci se păstrează între versiuni. Certificat OV, cu token hardware sau HSM (obligatoriu de la mijlocul lui 2023), câteva sute de euro pe an. ⚠️ De decis dacă se justifică pentru acest proiect — dar dacă se cumpără unul, acoperă toate utilitarele interne, nu doar acesta.
2. **Fără single-file, fără ILMerge/Costura, fără obfuscare, fără compresie.** Toate trei sunt tratate ca împachetare și cresc scorul euristic. DLL-urile stau alături de executabil.
3. **Cale de instalare stabilă** (ex. `C:\Program Files\ANCAFARM\ProcesorFacturi\`), nu `%TEMP%`, nu Desktop, nu folder de rețea. Executarea de pe share-uri de rețea crește scorul.
4. **Fără scriere de executabile la rulare** și fără pornire de procese externe. Aplicația doar citește și scrie fișiere de date.
5. Dacă apare totuși un fals pozitiv, se trimite la **Norton False Positive Submission** — rezolvarea vine în câteva zile și e permanentă pentru versiunea respectivă.

> Notă de context: reevaluarea Norton pentru mediul multi-site rămâne validă și independentă de acest proiect. Alegerea C# rezolvă problema pentru aplicația de față, nu și cauza mai generală.

### 14.5 Structura proiectului

Soluție unică, `ProcesorFacturi.sln`, cu separarea logicii de interfață — astfel logica rămâne testabilă fără GUI:

```
ProcesorFacturi.sln
├─ ProcesorFacturi.Core        (biblioteca de clase — fără referințe la WPF)
│   ├─ Config.cs               căi și opțiuni (§3.1, §9.1)
│   ├─ SurseFisiere.cs         scanare Tmp, mutare, arhivare (§3)
│   ├─ Registru.cs             InAnte/IeAnte: citire, deduplicare, adăugare,
│   │                          backup, scriere atomică, anulare lot (§4)
│   ├─ DetectorTip.cs          Intrări vs. Ieșiri din FurnizorCIF (§2)
│   ├─ Agregator.cs            consolidarea liniilor (§8)
│   ├─ ProcesorIesiri.cs       redenumire, remapare, prefixare (§6)
│   ├─ ProcesorIntrari.cs      redenumire, ClientNume, agregare (§7)
│   ├─ ScriitorXml.cs          (§10.1)
│   ├─ ScriitorXlsx.cs         (§10.2)
│   ├─ ScriitorDbf.cs          (§10.3) — BinaryWriter
│   ├─ Transliterare.cs        eliminarea diacriticelor (§10.3.1)
│   └─ Jurnal.cs               avertizări și erori, colectate pentru GUI și raport
├─ ProcesorFacturi.Tests       (teste unitare)
└─ ProcesorFacturi.App         (WPF — MainWindow.xaml, tab Registre, tab Grupe)
```

`ProcesorFacturi.Core` nu referențiază `PresentationFramework`. Jurnalul se colectează într-o listă returnată apelantului, nu se scrie direct în UI — altfel testele ar cere o fereastră.

Procesarea rulează pe `Task.Run`, cu `IProgress<T>` pentru actualizarea interfeței; nicio operație de I/O pe firul de UI.

### 14.6 Zone cu risc ridicat și ordinea de dezvoltare

De acoperit cu teste unitare **înainte** de prima rulare pe date reale:

- `Agregator.cs` — cazurile din §8.5, în special semnul cantității la storno și absorbirea liniilor de rotunjire; controlul de consistență din §8.6 este el însuși un test rulat în producție.
- `Registru.cs` — deduplicarea și anularea de lot, unde o eroare înseamnă fie facturi pierdute, fie duplicate în contabilitate.
- `ScriitorDbf.cs` — test de comparație **octet cu octet** împotriva fișierului de referință `IN_22-07-2026_23-07-2026_AF.dbf` (§10.3.2), ignorând doar cei 3 octeți de dată din antet. Acesta este cel mai valoros test din proiect.

Ordinea recomandată:

1. `Registru.cs` și `Agregator.cs` — pur funcționale, testabile fără GUI și fără fișiere reale;
2. `ScriitorDbf.cs`, validat pe fișierul de referință;
3. `ScriitorXml.cs` și `ScriitorXlsx.cs`;
4. GUI-ul;
5. `SurseFisiere.cs` **ultimul**, testat inițial pe copii — mutarea din `Tmp` (§3.2) este singura operație ireversibilă din aplicație.

### 14.7 Observații la portarea din v1

Codul v1 (`D:\nftosaga\procesor_facturi.py`) rămâne **referința funcțională**, nu și cea structurală: regulile de business se preiau din el, dar organizarea codului urmează §14.5. Diferențe de atenție la portare:

- `XDocument` normalizează spațierea la salvare; se încarcă cu `LoadOptions.PreserveWhitespace` și se salvează cu `SaveOptions.DisableFormatting` dacă formatul sursei trebuie păstrat.
- Declarația XML și encoding-ul fișierului de ieșire se preiau explicit din sursă (§10.1).
- În C#, `decimal.ToString()` respectă cultura curentă — vezi avertismentul din §14.3.
- Redenumirea unui element (`<CodArticolFurnizor>` → `<Descriere>`) nu există ca operație în LINQ to XML: se creează un `XElement` nou cu același conținut și se înlocuiește cel vechi, păstrând poziția în `<Linie>`.
