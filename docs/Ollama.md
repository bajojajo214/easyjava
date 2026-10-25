# AI w EasyJava — Ollama

W EasyJava 2.0.10 możesz opisać program zwykłymi słowami, a model z Ollamy przygotuje projekt z edytowalnymi bloczkami. Możesz też poprosić o zmianę w projekcie, który już masz. Zapis działa tak jak wcześniej: jeden plik `.js` zawiera program i projekt do dalszej edycji.

## Pierwsze uruchomienie

1. Zainstaluj i uruchom **[Ollamę dla Windows](https://ollama.com/download)**.
2. W EasyJava kliknij **AI • Ollama**. Domyślny adres to `http://127.0.0.1:11434`.
3. Odśwież listę i wybierz model zainstalowany w Ollamie. Możesz wpisać nazwę modelu ręcznie. Jeśli go jeszcze nie masz, kliknij **Pobierz model**. Aplikacja pobiera go dopiero na Twoje polecenie; potrzebny rozmiar dysku zależy od modelu.
4. Aby zrobić nowy program, odznacz **Popraw bieżący projekt**. Opisz program i kliknij **Wygeneruj**.
5. Sprawdź opis i drzewo proponowanych bloczków, a potem kliknij **Użyj projektu**.
6. Uruchom **F5**, sprawdź przyciski i zdarzenia, popraw bloczki według potrzeb. **Kompiluj na pulpit** utworzy gotowy `.js`.

Ollama i model są wymagane tylko do generowania. Ręczne tworzenie i uruchamianie zwykłych projektów działa bez AI. Po pobraniu lokalnego modelu możesz generować bez internetu. Pobieranie modelu wymaga dostępu do sieci.

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
| Generowanie trwa długo | Pierwsza odpowiedź wymaga załadowania modelu. Szybkość zależy od modelu i sprzętu. Możesz anulować operację i użyć mniejszego modelu lub prostszego opisu. |
| Odpowiedź nie jest prawidłowym projektem | Spróbuj ponownie, uprość polecenie lub wybierz inny model. Edytor nie wstawia nieprawidłowej propozycji. |
| Program działa inaczej niż chciałeś | Opisz konkretną poprawkę w trybie poprawiania bieżącego projektu albo edytuj bloczki ręcznie. |

Integracja korzysta z API Ollamy: [lista modeli](https://docs.ollama.com/api/tags), [generowanie odpowiedzi](https://docs.ollama.com/api/chat) i [odpowiedzi zgodne ze schematem JSON](https://docs.ollama.com/capabilities/structured-outputs). Szczegóły wykonanych testów i ich ograniczenia znajdziesz w [raporcie wydania](Raport-testow.txt).
