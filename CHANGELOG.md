# Historia zmian

## 2.0.12 — 2026-10-26

- Odpowiedź zakończona limitem długości jest sprawdzana: kompletny, poprawny projekt można przyjąć, a ucięty trafia do jednej ponownej próby z budżetem 8192 zamiast 4096 tokenów.
- Ponowienie używa prostszego formatu JSON zamiast dużego schematu generowania. Parser i kontrola poprawności projektu pozostają takie same; niedokończony wynik nie udostępnia przycisku Użyj projektu.
- Obie próby mają wspólny limit 5 minut. Anulowanie zatrzymuje także przejście do kolejnej próby; nie ma trzeciego żądania.
- Skrócono opis odpowiedzi w schemacie do 600 znaków i dodano do instrukcji modelu pełny przykład kalkulatora z odczytem pól, działaniami i obsługą przycisków.
- Walidacja propozycji wykrywa przyciski bez obsługi kliknięcia i nieobsługiwane operatory matematyczne. Schemat ogranicza operatory do dozwolonych wartości.
- W odpowiedzi AI usuwany jest błędny dodatkowy odwrotny ukośnik przed znakiem dzielenia `/`. Ta korekta dotyczy wyłącznie operatora `math`; nie zmienia tekstów, ścieżek ani komend.
- Nowy przycisk Gotowy kalkulator w panelu AI otwiera sprawdzony wzorzec bez połączenia z Ollamą. Dwa pola, cztery działania, Wyczyść, liczby ujemne i przecinki oraz komunikat przy dzieleniu przez zero. Wzorzec jest jawnie oznaczony i trafia do edytora dopiero przez Użyj projektu.
- Rozszerzono testy o ponowienie po limicie, poprawny JSON na końcu budżetu, anulowanie pomiędzy próbami oraz zapis, odczyt i kliknięcia kalkulatora wzorcowego.

## 2.0.11 — 2026-10-25

- Każdy rodzaj bloczka ma własny wariant schematu odpowiedzi AI, ograniczający mieszanie pól różnych bloczków.
- Kontrola brakujących identyfikatorów GUI, błędnych operandów i całkowicie zasłoniętych pól/przycisków przed udostępnieniem propozycji.
- Typowe odwołania AI do zadeklarowanych zmiennych w operandach liczbowych są zamieniane na format `{zmienna}`; zwykłe projekty, teksty, ścieżki i komendy pozostają dosłowne.

- Odpowiedź AI jest odbierana na żywo. Zakładka Postęp AI pokazuje etap pracy, upływ czasu, odebrane znaki, przybliżoną liczbę bloczków i ich rozpoznane nazwy.
- Gotowy, poprawny wynik przełącza panel do pełnej propozycji bloczków. Częściowa odpowiedź nie udostępnia przycisku Użyj projektu.
- Szczegóły błędów są widoczne w panelu postępu. Brak odpowiedzi lub zatrzymanie generowania kończy się czytelnym komunikatem.
- Jedna automatyczna prośba o poprawkę, gdy model zwróci bloczki o nieprawidłowej strukturze; bez wykonywania lub wstawiania błędnego wyniku.
- Przycisk Zmień model zachowuje opis zadania, a podczas generowania najpierw je przerywa. Wybór proponuje inne modele już pobrane oraz warianty do osobnego pobrania.
- Limity generowania: 5 minut łącznie, 90 sekund na pierwszy fragment i 45 sekund przerwy w odpowiedzi.
- Doprecyzowano instrukcje modelu dotyczące obliczeń i plików, aby ograniczyć mylenie pól bloczków.

## 2.0.10 — 2026-10-25

- Nowy panel AI • Ollama: opis programu po polsku zamieniany na edytowalny projekt z bloczkami.
- Lista modeli z lokalnego serwera, edytowalny adres i nazwa modelu oraz jawne pobieranie wybranego modelu przez Ollamę.
- Generowanie nowego projektu lub poprawianie bieżącego z zachowaniem zaimportowanych obrazów.
- Opis propozycji i drzewo bloczków przed wstawieniem; projekt uruchamia się dopiero na polecenie użytkownika.
- Wstawienie wygenerowanego projektu obsługuje cofanie. Zapis i kompilacja nadal tworzą pojedynczy plik .js.
- Generowanie i pobieranie można anulować; komunikaty pomagają przy niedostępnym serwerze lub modelu.
- AI jest opcjonalne. Zwykłe projekty i bloczki działają bez Ollamy.

## 2.0.9 — 2026-10-25

- Ikona dostarczona przez autora osadzona w pliku EXE, oknach, nagłówku edytora i instalatorze.
- Nowy EasyJava-Setup.exe: polski interfejs, instalacja offline do Program Files\EasyJava, skrót na pulpicie i w menu Start.
- Pakiet Windows Installer obsługuje odinstalowanie, naprawę transakcji i wykrywanie wcześniejszych/nowszych wersji.
- Instalator nie przejmuje rozszerzenia .js. Odinstalowanie zachowuje pliki spoza manifestu instalacji.
- Źródła instalatora buduje dostępny .NET Framework i makecab.exe, bez pobierania zewnętrznych narzędzi.

## 2.0.8 — 2026-10-25

- Nowe pole Działanie: Czekaj / W tle w CMD i PowerShell. W tle pozwala zapisywać TXT i wykonywać kolejne bloczki równocześnie z nieskończoną komendą, np. curl parrot.live.
- Zatrzymanie lub limit komendy w tle nie zatrzymuje innych zdarzeń; globalny Stop i zamknięcie aplikacji kończą aktywne komendy. Wyniki ze starego uruchomienia nie nadpisują nowego projektu.
- Pole tekstu w zapisie/dopisywaniu TXT obsługuje wiele wierszy. Potwierdzenie zapisu i pełna ścieżka trafiają do dziennika.
- Przykład papugi używa trybu W tle; stare komendy domyślnie zachowują Czekaj.
- 87 grup testów, wszystkie 102 bloczki i test rzeczywistego .js z komendą strumieniującą oraz zapisem TXT przed jej końcem.

## 2.0.7 — 2026-10-24

- Nowo upuszczany bloczek włącza swoją kategorię; starsze projekty mają widoczny przycisk włączenia pomijanych kategorii. Ręczne wyłączanie pozostaje dostępne.
- CMD, PowerShell i względne ścieżki plików używają folderu projektu; niezapisany podgląd używa pulpitu.
- Ścieżki obsługują {pulpit}, {dokumenty}, {folder_projektu}, zmienne środowiskowe i otaczające cudzysłowy. Komunikaty rozróżniają folder i plik oraz brak folderu zapisu.
- Wybór miejsca zapisu dopisuje rozszerzenie .txt i zachowuje folder roboczy aplikacji.
- Cofanie/ponawianie zachowuje folder otwartego projektu.
- Testy wszystkich 102 bloczków i rzeczywistego eksportu .js: CMD/PowerShell, kody/strumienie, zapis/dopisanie/odczyt TXT, bez trybu smoke.

## 2.0.6 — 2026-10-24

- Zakładka Terminal w wyskakującym oknie komendy wyświetla rzeczywisty wynik na żywo, z kolorami ANSI, pozycjonowaniem kursora, czyszczeniem i nadpisywaniem klatek.
- Polecenia takie jak curl parrot.live mogą wyświetlać animację w oknie GUI. Zakładki Wynik i Błędy zachowują podgląd tekstu.
- Poprawione odbieranie UTF-8 z CMD oraz zewnętrznych narzędzi; polecenie curl w PowerShell uruchamia systemowy program curl.exe.
- Ekran terminala ma ograniczony bufor i przewija tekst; wynik na żywo pozostaje aktualizowany także przy długim strumieniu.
- Zachowane zatrzymywanie, limit czasu, działanie w podglądzie i gotowym .js oraz kompilacja bez skrótów .lnk. Starszy .js należy otworzyć i zapisać w nowej wersji.

## 2.0.5 — 2026-10-24

- CMD i PowerShell otwierają osobne okno GUI z dosłowną treścią polecenia, folderem roboczym i limitem czasu.
- Zakładki Wynik i Błędy pokazują odbierany tekst na żywo. Po zakończeniu okno pozostaje otwarte z wynikiem i kodem zakończenia.
- Przycisk Zatrzymaj oraz zamknięcie aktywnego okna przerywają komendę. Wyniki nadal trafiają do zmiennych wskazanych w bloczku.
- Okno działa w podglądzie i wyeksportowanym .js. Kompilacja nadal tworzy sam plik .js na pulpicie, bez skrótów .lnk.

## 2.0.4 — 2026-10-24

- Kompilacja tworzy wyłącznie plik .js na pulpicie, bez skrótów .lnk.
- Nowa kategoria Terminal z bloczkami Uruchom polecenie CMD i Uruchom polecenie PowerShell: łącznie 102 bloczki w 12 kategoriach.
- Terminal włącza się w Funkcjach osobno dla projektu; jest domyślnie wyłączony także we wcześniejszych projektach. Ustawienie zapisuje się w pliku .js.
- Osobne zmienne na standardowy wynik, błędy i kod zakończenia, folder roboczy oraz limit czasu od 1 do 3600 sekund, domyślnie 30 s. Pusty folder roboczy oznacza folder użytkownika.
- Polecenia działają bez dodatkowego okna konsoli, nie blokują interfejsu i kończą się przed następnym bloczkiem. Stop i przekroczenie limitu czasu zatrzymują także procesy potomne.
- Składnia CMD/PowerShell, w tym cudzysłowy i klamry, jest zachowana; pole polecenia nie podstawia zmiennych EasyJava. Odbierany wynik i błędy mają osobne limity po milionie znaków.
- Obsługa działa w podglądzie i wyeksportowanym programie .js. Tryb --smoke pomija uruchamianie poleceń. EasyJava nie podnosi uprawnień ani nie zmienia zasad wykonywania PowerShell.

- Ręcznie wybierany przykład CMD i PowerShell z dwoma przyciskami i polem wyników; komendy wykonują się po kliknięciu.

## 2.0.3 — 2026-10-24

- Wyróżniony panel Przerwa między bloczkami bezpośrednio nad skryptami, z widocznym polem sekund. Zmiana pola od razu aktualizuje ustawienie projektu.
- Przycisk na górnym pasku ma czytelniejszą nazwę Przerwy i otwiera okno Przerwy między bloczkami z gotowymi czasami.
- Wyszukiwanie słów przerwa, pauza i odstęp znajduje bloczek Czekaj, który można przeciągnąć w konkretne miejsce skryptu.
- Instrukcja i README pokazują miejsce ustawiania przerw; numer wersji jest widoczny w tytule okna aplikacji.
- Zachowano zapis przerw w pliku .js, cofanie i ponawianie zmian oraz dotychczasowe zasady wykonywania i zatrzymywania skryptów.

## 2.0.2 — 2026-10-24

- Ustawienie Odstęp na górnym pasku: od 0 do 3600 sekund, dwa miejsca po przecinku oraz gotowe wartości 0 / 0,1 / 0,5 / 1 / 2 / 5 s.
- Odstęp zapisuje się w projekcie .js i działa w podglądzie oraz wyeksportowanym programie. Domyślna wartość 0 nie dodaje pauzy.
- Pierwsze polecenie każdego skryptu i nowego zdarzenia rusza od razu; kolejne czeka ustawiony czas od zakończenia poprzedniego.
- Pętle i warunki przekazują odstęp do swoich poleceń bez podwójnych pauz. Komentarze i wyłączone bloczki nie dodają czasu.
- Stop przerywa oczekiwanie, a bloczek Czekaj nadal dodaje swój własny czas.

## 2.0.1 — 2026-10-24

- Dodawanie z biblioteki wyłącznie przez przeciągnięcie i upuszczenie. Kliknięcie i dwuklik nie dodają bloczka.
- Widoczny bloczek pod kursorem oraz wskazanie miejsca upuszczenia.
- Poprawne wstawianie pomiędzy kartami, upuszczanie przez nagłówki i pola oraz do obu gałęzi warunku.
- Automatyczne przewijanie skryptów podczas przeciągania przy krawędzi.
- Unikalne identyfikatory kolejnych przeciąganych elementów GUI.
- Odrzucanie przeniesienia rodzica do własnego wnętrza i zagnieżdżonych deklaracji zdarzeń.
- Import obrazu osadza grafikę, a element obrazu dodaje się osobno przez upuszczenie bloczka.
- Testy regresji drag-and-drop i sprawdzenie prawdziwego przeciągania myszą.

## 2.0.0 — 2026-10-24

- 100 bloczków w 11 kategoriach, projektant GUI i podgląd działania.
- Samodzielny, edytowalny plik .js oraz uruchamianie bez Node.js.
- Duszki, zdarzenia, ruch, pisak, pliki, zmienne i listy.
- Pusty nowy projekt oraz ręcznie wybierane przykłady.
