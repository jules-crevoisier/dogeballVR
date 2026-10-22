# Envoi des builds sur Steam (SteamPipe)

Ce dossier contient les modèles de scripts SteamPipe et la marche à suivre pour envoyer DogeballVR sur Steam, depuis GitHub Actions (`.github/workflows/steam-upload.yml`) ou à la main avec steamcmd.

Rien ici ne fonctionne avant que jules ait un compte Steamworks, payé les frais Steam Direct et créé l'app (voir `docs/steam/ACTIONS-JULES.md`).

| Fichier | Rôle |
|---|---|
| `app_build.vdf.template` | Script d'app : AppID, description, branche à activer, liste des depots |
| `depot_build_windows.vdf.template` | Depot Windows 64 bits : exe, `steam_api64.dll`, dossier `data_DogeballVR_windows_x86_64` |
| `depot_build_linux.vdf.template` | Depot Linux 64 bits, facultatif (sans lui, le Steam Deck lance le build Windows avec Proton) |

Documentation Valve : https://partner.steamgames.com/doc/sdk/uploading

## 1. Jetons à remplacer

| Jeton | Valeur | Où la trouver |
|---|---|---|
| `__APPID__` | AppID du jeu, par exemple `3456780` | Steamworks > page d'accueil de l'app (l'URL contient l'AppID) |
| `__DEPOTID_WINDOWS__` | Depot Windows, en général AppID + 1 | Steamworks > Modifier les paramètres Steamworks > SteamPipe > Depots |
| `__DEPOTID_LINUX__` | Depot Linux, à créer à la main si on en veut un | Même page (bouton « Add New Depot »), puis Publier |
| `__DESC__` | Texte libre, visible seulement dans Steamworks | Le workflow met la version de `project.godot` et le commit |
| `__SETLIVE__` | Branche beta à activer après l'envoi, ou vide | Branches créées dans SteamPipe > Builds |
| `__PREVIEW__` | `1` = aperçu (aucun envoi), `0` = envoi réel | |

Le workflow remplace ces jetons tout seul à partir des secrets GitHub. À la main : copier les trois modèles dans un dossier `scripts/`, retirer `.template`, remplacer les jetons (et supprimer la ligne du depot Linux si on n'en a pas).

Arborescence attendue (chemins relatifs au script d'app) :

```
build/steam/
  scripts/app_build.vdf, depot_build_windows.vdf, depot_build_linux.vdf
  content/windows/  DogeballVR.exe, steam_api64.dll, data_DogeballVR_windows_x86_64/ (avec une 2e copie de steam_api64.dll)
  content/linux/    DogeballVR.x86_64, libsteam_api.so, data_DogeballVR_linuxbsd_x86_64/
  output/           journaux et caches de steamcmd
```

`steam_appid.txt` ne doit jamais être envoyé : les scripts de depot l'excluent. Le zip de test de `export.yml` en contient un (480, Spacewar) pour essayer les fonctions Steam sans compte Steamworks.

## 2. Compte builder dédié (recommandé par Valve)

Ne pas envoyer les builds avec le compte de jules. Créer un compte Steam séparé qui ne sert qu'à ça :

1. Créer un compte Steam neuf (adresse mail dédiée, par exemple `builds+dogeball@…`), sans moyen de paiement.
2. Dans Steamworks, Utilisateurs et permissions > Gérer les utilisateurs : inviter ce compte, lui donner seulement l'accès à l'app DogeballVR avec les permissions **Edit App Metadata** et **Publish App Changes To Steam**.
3. Steam Guard : le laisser actif (par mail ou appli mobile). Pour une app déjà sortie, Valve demande un téléphone ou l'appli Steam Mobile sur le compte qui active un build, et impose 3 jours d'attente après un changement de sécurité du compte (mail, téléphone) avant de pouvoir activer un build.

## 3. Steam Guard sans intervention : `config.vdf`

GitHub Actions ne peut pas saisir un code Steam Guard. On se connecte donc une fois à la main, et on donne au workflow le fichier `config.vdf` qui contient le jeton de connexion.

Sur un PC Linux ou WSL (le chemin diffère sous Windows, voir plus bas) :

```bash
mkdir -p ~/Steam && cd ~/Steam
curl -sSL https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz | tar -xz
./steamcmd.sh +login NOM_DU_COMPTE_BUILDER +quit     # saisir le mot de passe puis le code Steam Guard
base64 -w0 ~/Steam/config/config.vdf > config.vdf.b64  # contenu à coller dans le secret STEAM_CONFIG_VDF
```

Sous Windows avec `steamcmd.exe`, le fichier est `<dossier de steamcmd>\config\config.vdf` ; l'encoder avec PowerShell :
`[Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\steamcmd\config\config.vdf")) | Set-Clipboard`.

Le jeton finit par expirer, ou est invalidé si on se reconnecte avec le mot de passe ailleurs. Si le workflow affiche « Connexion Steam refusée », refaire cette section et remplacer le secret. Supprimer `config.vdf.b64` du disque une fois le secret enregistré.

## 4. Secrets GitHub

Dépôt GitHub > Settings > Environments > New environment, nommé `steam-production` : « Required reviewers » = jules, « Deployment branches and tags » = `main` seulement. Puis, dans cet environnement, « Add environment secret » pour chaque ligne ci-dessous. Rangés là plutôt qu'en secrets de dépôt, ils ne sont lisibles par aucune autre branche, même si elle modifie sa copie du workflow.

| Secret | Obligatoire | Contenu |
|---|---|---|
| `STEAM_USERNAME` | oui | Nom de connexion du compte builder |
| `STEAM_CONFIG_VDF` | oui | `config.vdf` encodé en base64 (section 3) |
| `STEAM_APP_ID` | oui | AppID |
| `STEAM_DEPOT_ID_WINDOWS` | oui | Depot Windows |
| `STEAM_DEPOT_ID_LINUX` | non | Depot Linux : s'il existe, le workflow exporte et envoie aussi Linux |

Si un secret obligatoire manque, le workflow s'arrête dès la première étape, avant tout export, avec la liste des secrets manquants.

## 5. Lancer un envoi

Onglet Actions > « Envoi Steam » > Run workflow :

- **branche** : `beta` par défaut. Le build est envoyé puis activé sur cette branche (à créer d'abord dans SteamPipe > Builds, éventuellement avec un mot de passe pour les testeurs). Vide = envoyé sans être activé.
- **description** : texte libre (sinon version + commit).
- **apercu** : coché = steamcmd vérifie et liste les fichiers sans rien envoyer.

Le résumé du run donne le BuildID. Les journaux steamcmd sont gardés 14 jours dans l'artefact `steam-build-logs`.

## 6. Branches beta et default

- **default** : ce que reçoivent tous les joueurs. Steam refuse de l'activer automatiquement (« the default branch can not be set live automatically ») : après validation sur `beta`, l'activer à la main dans SteamPipe > Builds (choisir le build, « Set build live on branch default », puis confirmer).
- **beta** (ou `qa`, `playtest`…) : branches de test. Les testeurs la choisissent dans Steam > Propriétés du jeu > Bêtas, avec un mot de passe si la branche en a un.
- Avant la sortie, le build de `default` doit avoir passé la revue de build de Valve (3 à 5 jours ouvrés, prévoir 7).

## 7. Envoi à la main (sans GitHub)

```bash
# Depuis la racine du dépôt, après un export Godot dans build/windows (voir export.yml).
mkdir -p build/steam/{scripts,content/windows,output}
cp -r build/windows/DogeballVR.exe build/windows/steam_api64.dll build/windows/data_DogeballVR_windows_x86_64 build/steam/content/windows/
sed -e 's/__APPID__/3456780/g' -e 's/__DEPOTID_WINDOWS__/3456781/g' -e 's/__DESC__/test manuel/' \
    -e 's/__SETLIVE__/beta/' -e 's/__PREVIEW__/0/' -e '/__DEPOTID_LINUX__/d' tools/steam/app_build.vdf.template > build/steam/scripts/app_build.vdf
sed -e 's/__DEPOTID_WINDOWS__/3456781/g' tools/steam/depot_build_windows.vdf.template > build/steam/scripts/depot_build_windows.vdf
~/Steam/steamcmd.sh +login NOM_DU_COMPTE_BUILDER +run_app_build "$PWD/build/steam/scripts/app_build.vdf" +quit
```

(Remplacer 3456780 / 3456781 par les vrais identifiants.) `steam_api64.dll` vient de https://github.com/rlabrecque/Steamworks.NET/releases/download/2024.8.0/Steamworks.NET-Standalone_2024.8.0.zip, dossier `Windows-x64/` ; en mettre une copie à côté de l'exe et une dans `data_DogeballVR_windows_x86_64/`.
