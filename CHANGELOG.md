# Historia zmian

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
