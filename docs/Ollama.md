# AI w EasyJava — Ollama

W EasyJava 2.0.11 możesz opisać program zwykłymi słowami, a model z Ollamy przygotuje projekt z edytowalnymi bloczkami. Możesz też poprosić o zmianę w projekcie, który już masz. Zapis działa tak jak wcześniej: jeden plik `.js` zawiera program i projekt do dalszej edycji. Panel pokazuje postęp na żywo i pomaga wybrać inny model, gdy odpowiedź nie powstaje lub jest błędna.

## Pierwsze uruchomienie

1. Zainstaluj i uruchom **[Ollamę dla Windows](https://ollama.com/download)**.
2. W EasyJava kliknij **AI • Ollama**. Domyślny adres to `http://127.0.0.1:11434`.
3. Odśwież listę i wybierz model zainstalowany w Ollamie. Możesz wpisać nazwę modelu ręcznie. Jeśli go jeszcze nie masz, kliknij **Pobierz model**. Aplikacja pobiera go dopiero na Twoje polecenie; potrzebny rozmiar dysku zależy od modelu.
4. Aby zrobić nowy program, odznacz **Popraw bieżący projekt**. Opisz program i kliknij **Wygeneruj**.
5. Obserwuj zakładkę **Postęp AI**. Gdy generowanie się skończy, panel pokaże pełną **Propozycję bloczków**. Sprawdź opis i drzewo, a potem kliknij **Użyj projektu**.
6. Uruchom **F5**, sprawdź przyciski i zdarzenia, popraw bloczki według potrzeb. **Kompiluj na pulpit** utworzy gotowy `.js`.

Ollama i model są wymagane tylko do generowania. Ręczne tworzenie i uruchamianie zwykłych projektów działa bez AI. Po pobraniu lokalnego modelu możesz generować bez internetu. Pobieranie modelu wymaga dostępu do sieci.

**Instalator EasyJava nie instaluje automatycznie Ollamy ani modeli.** Przycisk **Pobierz model** pobiera wskazany model do działającego serwera Ollamy.

## Co pokazuje Postęp AI

Po rozpoczęciu generowania panel przełącza się do zakładki **Postęp AI**. Widać:

- etap pracy, np. przygotowanie modelu, tworzenie bloczków lub poprawianie odpowiedzi;
- czas od rozpoczęcia generowania;
- liczbę odebranych znaków i przybliżoną liczbę powstających bloczków;
- nazwy bloczków rozpoznane w ostatnim fragmencie odpowiedzi.

Licznik bloczków opisuje napływający tekst, a nie procent ukończenia. Częściowa odpowiedź pozostaje podglądem roboczym. Przycisk **Użyj projektu** jest dostępny dopiero po odebraniu i sprawdzeniu pełnego projektu.

Wczytanie modelu i przygotowanie polecenia może zająć chwilę. Po dłuższej przerwie bez nowych danych panel wyjaśnia, że nadal czeka, i podpowiada zmianę modelu. Sam brak pierwszego fragmentu przez kilkanaście sekund nie oznacza, że model jest uszkodzony.

EasyJava czeka do **90 sekund na pierwszy fragment** i do **45 sekund na kolejne dane po zatrzymaniu strumienia**. Całe generowanie, razem z ewentualną poprawką, ma limit **5 minut**. Po przekroczeniu limitu lub przerwaniu połączenia zobaczysz szczegóły; niedokończony projekt nie trafi do edytora.

## Błędna odpowiedź i zmiana modelu

Model może pomylić typ bloczka, jego pola lub zagnieżdżenie. EasyJava sprawdza te elementy przed wstawieniem. Jeżeli odpowiedź jest nieprawidłowa, program prosi ten sam model o **jedną poprawkę**, przekazując mu opis zadania i wskazany błąd. Etap poprawiania zobaczysz w panelu postępu.

Gdy poprawka także jest błędna, w **Postępie AI** zobaczysz wyjaśnienie problemu i propozycję **Zmień model**. Możesz też wybrać ten przycisk samodzielnie podczas długiego generowania: bieżąca operacja zostanie przerwana, a opis zadania pozostanie w polu.

Okno wyboru pokazuje najpierw inne modele już pobrane na wybranym serwerze, a następnie alternatywy do osobnego pobrania. Nie proponuje ponownie bieżącego modelu. Przykładowe warianty:

| Model | Pobranie w przybliżeniu | Kiedy spróbować |
| --- | --- | --- |
| `qwen2.5-coder:7b` | 4,7 GB | Większy model kodowy może lepiej poradzić sobie z trudniejszym projektem; wymaga więcej pamięci i może działać wolniej. |
| `qwen2.5-coder:3b` | 1,9 GB | Mniejszy model do codziennych, niezbyt dużych projektów. |
| `qwen2.5-coder:1.5b` | Około 1 GB | Lżejszy wariant do prostych zadań, gdy brakuje pamięci; może częściej popełniać błędy. |

Rozmiary dotyczą pobrania plików, a nie całej pamięci potrzebnej do pracy modelu. Źródło: [warianty Qwen2.5-Coder w Ollamie](https://ollama.com/library/qwen2.5-coder/tags).

Kliknięcie **Wybierz model** zachowuje opis, ale **nie pobiera modelu automatycznie**. Jeśli model jest już pobrany, kliknij **Wygeneruj**. W przeciwnym razie najpierw użyj **Pobierz model**, a po pobraniu **Wygeneruj**. Zmiana modelu może dać inny wynik, lecz nie gwarantuje poprawnego programu. Wygenerowane bloczki nadal warto sprawdzić przez F5.

## Co wpisać w poleceniu

Opisz elementy okna, ich zachowanie i oczekiwany wynik. Dobrze zacząć od małej aplikacji, sprawdzić ją, a potem poprosić o dalsze zmiany.

Przykłady:

- „Zrób licznik kliknięć. Okno ma mieć napis Wynik: 0, przycisk +1 i przycisk Wyzeruj. Po każdym kliknięciu odśwież wynik”.
- „Zrób listę zadań: pole do wpisania zadania, przycisk Dodaj, lista zadań i przycisk Wyczyść listę”.
- „Utwórz duszka robota. Strzałki mają go przesuwać, a spacja zmieniać kolor. Dodaj instrukcję sterowania na górze okna”.
- „Zrób notatnik z dużym polem tekstowym i przyciskami Otwórz oraz Zapisz. Przy zapisie zapytaj o plik TXT”.

Model otrzymuje opisy dostępnych bloczków EasyJava. Wynik jest projektem do edycji w tym programie. Nie każdą funkcję dowolnego języka da się wyrazić dostępnymi bloczkami.

## Poprawianie bieżącego projektu

Otwórz swój `.js`, wejdź w **AI • Ollama** i zaznacz **Popraw bieżący projekt**. Opisz zmianę, np. „Dodaj do licznika przycisk -1 i nie pozwól zejść poniżej zera”.

Do modelu trafiają bloczki bieżącego projektu jako kontekst. Zaimportowane obrazy pozostają w projekcie. Przed wstawieniem zobaczysz pełną propozycję. Samo generowanie nie zmienia projektu w edytorze; robi to dopiero **Użyj projektu**. Wstawienie możesz cofnąć przyciskiem **Cofnij**.

## Podgląd i uruchamianie

Generator pokazuje strukturę bloczków i opis odpowiedzi. Nie uruchamia wygenerowanych bloczków ani komend. Po wstawieniu uruchom podgląd samodzielnie i sprawdź, czy program robi to, co chciałeś.

Jakość odpowiedzi zależy od modelu oraz opisu. Gdy efekt jest inny niż oczekiwany, doprecyzuj polecenie w trybie poprawiania albo zmień bloczki ręcznie. Zmiana modelu może również dać inny wynik.

## Połączenie i dane

Domyślny serwer to lokalna Ollama pod `http://127.0.0.1:11434`. EasyJava nie wymaga klucza API do lokalnej Ollamy. W panelu możesz podać własny adres serwera HTTP lub HTTPS.

Opis programu trafia do wybranego serwera. W trybie poprawiania projektu trafiają tam również jego bloczki. Jeśli podasz adres innego komputera lub usługi, dane otrzyma ten serwer. Użycie modelu lub usługi w chmurze może wymagać osobnej konfiguracji w Ollamie.

## Gdy coś nie działa

| Objaw | Co zrobić |
| --- | --- |
| Brak połączenia z serwerem | Uruchom Ollamę, sprawdź adres i odśwież listę modeli. Domyślny port to `11434`. |
| Lista modeli jest pusta | Wpisz nazwę modelu i kliknij **Pobierz model**, a potem odśwież listę. |
| Model nie istnieje | Sprawdź pełną nazwę i wariant modelu, także część po `:`. Wybierz model dostępny na tym serwerze. |
| Generowanie trwa długo | Otwórz **Postęp AI** i sprawdź czas oraz napływ znaków. Możesz kliknąć **Zmień model**, co najpierw przerwie operację, albo uprościć opis. |
| Model przestał przesyłać dane | Panel wykrywa przerwę i kończy oczekiwanie po limicie. Zobacz szczegóły w **Postępie AI** i spróbuj innego modelu. |
| Odpowiedź nie jest prawidłowym projektem | EasyJava prosi o jedną poprawkę. Jeśli także się nie uda, pokazuje błąd i podpowiada **Zmień model**. Edytor nie wstawia nieprawidłowej propozycji. |
| Program działa inaczej niż chciałeś | Opisz konkretną poprawkę w trybie poprawiania bieżącego projektu albo edytuj bloczki ręcznie. |

Integracja korzysta z API Ollamy: [lista modeli](https://docs.ollama.com/api/tags), [generowanie odpowiedzi](https://docs.ollama.com/api/chat) i [odpowiedzi zgodne ze schematem JSON](https://docs.ollama.com/capabilities/structured-outputs). Szczegóły wykonanych testów i ich ograniczenia znajdziesz w [raporcie wydania](Raport-testow.txt).
