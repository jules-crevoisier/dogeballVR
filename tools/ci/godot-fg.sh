#!/usr/bin/env bash
# Lance Godot au premier plan, sortie dans un journal : tools/ci/godot-fg.sh JOURNAL GODOT ARGS...
# Godot 4.3 .NET plante parfois en libérant le moteur, après la fin du jeu (Abort 134, SIGSEGV 139) :
# si le jeu a écrit sa ligne de fermeture propre, ce plantage ne fait pas échouer le test, qui juge le journal ;
# ce que le moteur écrit en se libérant est mis à part dans JOURNAL.exit. Un plantage avant cette ligne reste un échec.
log="$1"; shift
"$@" > "$log" 2>&1
rc=$?
if { [ $rc -eq 134 ] || [ $rc -eq 139 ]; } && grep -q "\[Crash\] Fermeture propre" "$log"; then
  echo "::warning::$log : plantage du moteur à la fermeture (code $rc) après la fin du jeu, ignoré"
  line=$(grep -n "\[Crash\] Fermeture propre" "$log" | tail -1 | cut -d: -f1)
  tail -n +"$((line + 1))" "$log" > "$log.exit"
  head -n "$line" "$log" > "$log.tmp" && mv "$log.tmp" "$log"
  exit 0
fi
if [ $rc -ne 0 ]; then echo "Le client s'est arrêté (code $rc), fin du journal :"; tail -40 "$log"; fi
exit $rc
