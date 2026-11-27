# Pack Démo EVA VR — Guide de présentation

Ce guide rassemble tout ce qu'il faut pour lancer une démonstration **hybride Meta Quest + PC** propre et sans friction devant l'équipe EVA.

---

## 1. Les 2 arènes vitrines

| Arène | Identifiant | Format | Points forts à montrer |
| :--- | :--- | :--- | :--- |
| **Croix Néon** | `croix_neon` | 2v2 | Arène lumineuse en plein jour, rotor central dynamique, lecture immédiate des équipes Orange vs Bleu et des balles. |
| **Fosse Croisée** | `fosse_croisee` | 2v2 | Fosse centrale, tremplins et plateformes en hauteur, ring-outs spectaculaires. |

---

## 2. Lancement en 1 commande (jour J)

### PC Hôte / Opérateur (LAN + mode démo)
- **Windows** :
  ```bat
  tools\eva-demo.bat host croix_neon
  ```
- **Linux / macOS** :
  ```bash
  ./tools/eva-demo.sh host croix_neon
  ```
- **Vitrine écran géant (bots autonomes + caméra aérienne)** pendant l'accueil :
  ```bash
  ./tools/eva-demo.sh spectator croix_neon
  ```

### Casque Meta Quest (rejoint le PC hôte en LAN)
1. Brancher le Quest et pousser les réglages de salle EVA (`eva.cfg`) :
   ```bash
   ./tools/eva-demo.sh quest-cfg
   ```
2. Lancer l'APK sur le Quest en pointant sur l'IP locale du PC hôte (`--join=<IP_PC> --zone=1`), ou passer par la salle LAN (`En ligne -> Rejoindre par IP`).
3. Dès qu'un casque VR et un joueur PC partagent la même partie, le mode **hybride équilibré** s'active automatiquement (vitesse max des tirs PC plafonnée à `24 m/s`, fenêtre d'attrape et passes alliées unifiées façon *Knockout City*).

---

## 3. Raccourcis opérateur en match (`--eva-demo`)

| Touche | Action |
| :---: | :--- |
| **F6** | Relancer immédiatement une nouvelle partie sur la même arène (zéro retour au menu). |
| **F7** | Basculer instantanément entre **Croix Néon** et **Fosse Croisée**. |
| **F8** | Basculer entre la **caméra joueur** et la **caméra spectateur TV** (vue orbitale en hauteur pour l'écran de la salle). |
| **F10** | Afficher ou masquer le bandeau discret des raccourcis opérateur en haut à gauche. |

---

## 4. Check-list avant l'arrivée du patron d'EVA

- [ ] PC hôte et casque(s) Quest sur le même Wi-Fi / LAN (port UDP par défaut ouvert dans le pare-feu Windows).
- [ ] `eva.cfg` poussé sur le(s) casque(s) Quest (`haptics=85`, `volume=85`, `end_hold=15`).
- [ ] Lancer `eva-demo` sur `croix_neon` et vérifier `F6` / `F7` / `F8`.
- [ ] Garder une manette Xbox/PS ou clavier-souris prêt sur le PC pour jouer en équipe ou en face du casque VR (démonstration du cross-play hybride).

---

## 5. Bilan performances mesuré (`--perf-log`, objectif Quest 72 FPS = 13,9 ms/image)

Mesures relevées sur 30 s de match complet (`--pc-autoplay --perf-log`) :

| Arène | Moyenne | p95 | Nœuds max | Mémoire moteur | Tas .NET | Nœuds orphelins |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **Croix Néon** | 141 i/s | **8,9 ms** | 1 555 | 69,9 Mo | 8,2 Mo | **0** |
| **Fosse Croisée** | 141 i/s | **8,7 ms** | 1 701 | 67,2 Mo | 5,5 Mo | **0** |

> [!TIP]
> Sur casque Quest, `ModelLibrary.Quest` active automatiquement les variantes allégées des modèles et `GcPacer` lisse le ramasse-miettes pour éviter les pics en pleine partie.
