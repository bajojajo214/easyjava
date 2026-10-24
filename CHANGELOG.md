# Historia zmian

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
