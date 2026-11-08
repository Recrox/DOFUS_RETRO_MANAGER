# Miroir des mises à jour (Cloudflare)

> **Réservé au mainteneur.** Les joueurs n'ont rien à faire ici.

L'application vérifie les nouvelles versions sur GitHub **et** sur ce miroir : https://drm-update.recrox.workers.dev. Quand GitHub ne répond pas (503, 504, limite de requêtes), la mise à jour passe par Cloudflare.

Le miroir est un Worker Cloudflare sans script, qui ne sert que des fichiers statiques :

- `version.json` : version, empreinte SHA-256, taille et liste des morceaux de l'installateur ;
- `DofusRetroManager-Setup-<version>.msi.0`, `.1`… : l'installateur découpé en morceaux de 20 Mo (un Worker refuse les fichiers de plus de 25 Mo). L'application les télécharge un par un, les recolle et vérifie l'empreinte.

Il ne reçoit ni ne garde aucune donnée du joueur (règle 4 de [REGLES.md](../REGLES.md)).

## Mise en place (une seule fois)

Le Worker `drm-update` est déjà créé. Le workflow de Release le met à jour à chaque version, à condition d'avoir le secret `CLOUDFLARE_API_TOKEN` :

1. Cloudflare : https://dash.cloudflare.com/profile/api-tokens › **Create Token** › modèle **Edit Cloudflare Workers** › compte `Recrox91@hotmail.com's Account`, zone : *All zones* › **Continue to summary** › **Create Token**. Copiez le jeton.
2. GitHub : dépôt › **Settings** › **Secrets and variables** › **Actions** › **New repository secret** : nom `CLOUDFLARE_API_TOKEN`, valeur : le jeton.

Sans ce secret, l'étape est ignorée et l'application se met à jour par GitHub seulement.

## Publier à la main

Depuis ce dossier, après `npx wrangler login` : déposez les morceaux et `version.json` dans `public/` (voir l'étape « Publier sur le miroir Cloudflare » du workflow), puis `npx wrangler deploy`.
