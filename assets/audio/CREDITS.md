# Sons et musiques : sources et licences

Tous les fichiers de `assets/audio/` sont sous licence **CC0 1.0 (domaine public)** ou ont été synthétisés pour le projet.
Licence vérifiée sur la page de chaque source (Kenney : fichier `License.txt` du pack et page kenney.nl ; OpenGameArt : licence
affichée sur la page, recherche filtrée CC0). Aucune attribution n'est exigée ; on crédite quand même les auteurs.

Les fichiers ont été retravaillés par `build_audio.py` (mixage de couches, mono pour les sons spatialisés, crête à -3 dBFS
pour les effets, -14 LUFS intégrés pour la musique, Ogg Vorbis q4). Relancer : `python3 build_audio.py <téléchargements> assets/audio`.

## Packs

| Source | Auteur | Licence | Page |
|---|---|---|---|
| Impact Sounds | Kenney | CC0 | https://kenney.nl/assets/impact-sounds |
| Interface Sounds | Kenney | CC0 | https://kenney.nl/assets/interface-sounds |
| UI Audio | Kenney | CC0 | https://kenney.nl/assets/ui-audio |
| Sci-Fi Sounds | Kenney | CC0 | https://kenney.nl/assets/sci-fi-sounds |
| Music Jingles | Kenney | CC0 | https://kenney.nl/assets/music-jingles |
| Swishes Sound Pack | artisticdude | CC0 | https://opengameart.org/content/swishes-sound-pack |
| Applause in a large hall | eXpl0it3r | CC0 | https://opengameart.org/content/applause-in-a-large-hall-or-church |
| High traffic road sounds | IgnasD | CC0 | https://opengameart.org/content/high-traffic-road-sounds |
| Funky Menu Loop | iamoneabe | CC0 | https://opengameart.org/content/funky-menu-loop |
| Simple Action Beat | LushoGames | CC0 | https://opengameart.org/content/simple-action-beat |
| Funky Victory Loop | Archonic | CC0 | https://opengameart.org/content/funky-victory-loop |

## Effets (`sfx/<kind>_N.ogg`)

| Fichiers | Composition |
|---|---|
| throw_1..4 | swish-5/7/9/6 (Swishes) + même swish ralenti ×0,62 à -7 dB (corps du lancer) |
| catch_1..4 | impactPunch_medium_000..003 + impactSoft_medium_000..003 à -5 dB (claque dans les gants) |
| perfect_1..3 | impactPunch_heavy_000/002/003 + confirmation_004 (Interface) à -7 dB, décalé de 30 ms |
| tier_1..3 | maximize_001/002/006 (Interface) |
| ko_1..2 | impactPunch_heavy_002/003 + lowFrequency_explosion_001 (Sci-Fi) à -4 dB |
| alert_1..2 | pluck_001/002 (Interface) |
| parry_1..3 | impactPlate_medium_000/001/004 + impactPunch_medium à -5 dB |
| bomb_1..3 | explosionCrunch_000..002 + lowFrequency_explosion_000 (Sci-Fi) à -3 dB |
| hit_1..4 | impactPunch_heavy + impactSoft_heavy (balle caoutchouc lourde) |
| click_1..3 | click1..3 (UI Audio) |
| uihover_1..3 | rollover1..3 (UI Audio) |
| uiclick_1..2 | select_001/002 (Interface) |
| uiback_1..2 | back_001/002 (Interface) |
| countdown_1 | bong_001 (Interface) |
| go_1 | coup de sifflet court (synthèse) + confirmation_004 à -6 dB |
| whistle_1..2 | sifflet d'arbitre à bille, « tuut-tuuuut » et long (synthèse : 2,95 kHz, trille 32 Hz, souffle filtré) |
| dash_1..3 | swish-3/8/9 ralentis ×0,72 + ×0,5 à -6 dB |
| jump_1..3 | swish-1/2/4 accélérés ×1,12 |
| land_1..3 | impactSoft_medium_000..002 + footstep_concrete_000..002 à -3 dB |
| chargetick_1 | tick_004 (Interface) |
| koconfirm_1..2 | jingles_HIT13/HIT16 (Music Jingles) |
| victory_1 / defeat_1 | jingles_SAX02 (montant) / jingles_SAX01 (descendant) |
| hurtself_1..2 | impactPunch_heavy + impactSoft_heavy, passe-bas 2–3 kHz (coup reçu, étouffé) |
| bounce_1..3 | impactSoft_medium accéléré ×1,15 + impactGeneric_light à -6 dB (rebond au sol) |
| crowd_1..3 | 3 extraits de 2,6 s d'Applause in a large hall (les plus fournis), fondus |
| low_1..2 | coup grave synthétisé (sinus 120→42 Hz / 95→36 Hz), couche ajoutée sous Hit, Ko, HurtSelf, Bomb, Perfect |

## Musique et ambiance

| Fichier | Source |
|---|---|
| music/menu.ogg | Funky Menu Loop, version longue (iamoneabe), boucle de 36,9 s (20 mesures) |
| music/match.ogg | Simple Action Beat (LushoGames), boucle de 120 s |
| music/victory.ogg | Funky Victory Loop (Archonic), jouée une fois avec fondu final |
| music/defeat.ogg | jingles_STEEL06 (Kenney Music Jingles) |
| ambience/rooftop.ogg | High traffic road sounds (IgnasD), passe-bas 1,6 kHz (circulation en contrebas du toit), boucle de 40 s par fondu enchaîné de 3 s, -26 LUFS |
