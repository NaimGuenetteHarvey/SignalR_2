# 📘 Aide-mémoire SignalR : EXAMEN

> Fonctionne pour **n'importe quel projet** : chat, jeu, enchères, notifications...
> Lis la **section 1** pour comprendre, puis utilise les **sections 3 à 6** comme recettes.

---

## 1. L'idée en 30 secondes : le restaurant 🍽️

| Restaurant | SignalR | Où |
|---|---|---|
| Le **client** à table | React | `.tsx` |
| La **cuisine** | Le Hub | `XxxHub.cs` |
| Le **menu** | Les méthodes `public` du Hub | `XxxHub.cs` |
| **Commander** un plat | `invoke('Plat', ...)` | React |
| La cuisine **annonce** à voix haute | `Clients.___.SendAsync("Mot", ...)` | Hub |
| **Écouter** une annonce | `on('Mot', ...)` | React |

```
REACT                                         HUB C#
invoke('CreateChannel', "Jeux")   ───────►   public async Task CreateChannel(string title)
  « je commande »                               ... travaille (BD, services) ...
on('ChannelsList', data => ...)   ◄───────   Clients.All.SendAsync("ChannelsList", liste)
  « j'écoute »                                 « j'annonce »
```

### Les 2 règles d'or
1. **`invoke`** : le nom = **le nom exact d'une méthode du Hub** (imposé).
   - Un nom faux donne une **ERREUR**.
2. **`SendAsync` ↔ `on`** : le nom = **un mot que tu inventes**, mais **identique des deux côtés**.
   - Un nom différent donne **RIEN** : aucune erreur, juste rien ne se passe.

---

## 2. Les outils du Hub (à savoir par cœur)

### `Context` : QUI m'appelle ?
| Code | Donne | Sert à |
|---|---|---|
| `Context.UserIdentifier` | id de l'**utilisateur** | savoir qui agit, `Clients.User(...)`, chercher en BD |
| `Context.ConnectionId` | id de **cet onglet** | `Groups.Add/Remove...`, `Clients.Client(...)` |

> Un utilisateur avec 2 onglets = **1** UserIdentifier, **2** ConnectionId.

### `Clients` : À QUI j'envoie ?
| Code | Qui reçoit |
|---|---|
| `Clients.Caller` | seulement celui qui a appelé |
| `Clients.All` | tout le monde |
| `Clients.Others` | tout le monde sauf l'appelant |
| `Clients.Group("nom")` | tous ceux dans le groupe |
| `Clients.OthersInGroup("nom")` | le groupe sauf l'appelant |
| `Clients.User(userId)` | tous les onglets d'**un utilisateur** (prend un **UserIdentifier**) |
| `Clients.Client(connectionId)` | **un onglet** précis (prend un **ConnectionId**) |

### `Groups` : RANGER des onglets dans des salles
```csharp
await Groups.AddToGroupAsync(Context.ConnectionId, "nom");       // entrer
await Groups.RemoveFromGroupAsync(Context.ConnectionId, "nom");  // sortir
```
> « Joindre / quitter » une salle, un canal ou une partie = **`Groups`**, **pas** `SendAsync`.
> Le 1er paramètre est **toujours un ConnectionId** (jamais un email ou un nom).

### Les méthodes automatiques (jamais appelées par `invoke`)
```csharp
public override async Task OnConnectedAsync()                 { ... }  // quelqu'un se connecte
public override async Task OnDisconnectedAsync(Exception? e)  { ... }  // quelqu'un part
```

---

## 3. RECETTE : écrire une méthode du Hub

Réponds aux **6 questions**, dans l'ordre :

| # | Question | Donne |
|---|---|---|
| ❶ | Que doit m'envoyer le client ? | les **paramètres** de la méthode |
| ❷ | Qui fait l'action ? | `Context.UserIdentifier` (**jamais** un paramètre !) |
| ❸ | Est-ce permis ? | `if (...) throw new HubException("message");` |
| ❹ | Qu'est-ce que je fais ? | appel d'un service / BD / `Groups` |
| ❺ | **Qui doit le savoir ?** | `Clients.___` → **le plus petit groupe possible** |
| ❻ | Quel mot pour l'annonce ? Quelle donnée ? | `SendAsync("Mot", donnée)` |

```csharp
public async Task NomAction(int id, string texte)                       // ❶
{
    string userId = Context.UserIdentifier!;                            // ❷
    if (texte == "") throw new HubException("Texte vide");              // ❸
    var resultat = await _monService.Faire(userId, id, texte);          // ❹
    await Clients.Group("salle-" + id)                                  // ❺
                 .SendAsync("NomEvenement", resultat);                  // ❻
}
```

### Choisir le destinataire (❺)
| Situation | `Clients.___` |
|---|---|
| Ça concerne **tout le monde** (liste globale qui change) | `All` |
| Seulement **moi** (réponse, données initiales) | `Caller` (ou `return`) |
| Une **salle / partie / canal** | `Group(nom)` |
| Une **personne précise** (message privé) | `User(userId)` |
| Un **onglet précis** dont j'ai gardé l'id | `Client(connectionId)` |

### Choisir le mot de l'annonce (❻)
1. **Le client écoute déjà un `on` qui fait ce que je veux ?** → je **réutilise son nom**.
   - Exemple : afficher un texte → `"NewMessage"`.
2. **Sinon** → j'**invente** un nom clair (`ChannelsList`, `LeaveChannel`, `ScoreUpdated`) et j'écris le `on` avec le **même**.
3. ❌ Jamais le nom de la **méthode** (`SendMessage`) comme nom d'**événement**.
4. ❌ Jamais un nom flou (`"message"`) ni un nom déjà utilisé pour autre chose.

### Choisir la donnée (❻)
| Le client doit... | J'envoie |
|---|---|
| remplacer toute une liste | **la liste complète à jour** |
| ajouter un élément | **l'élément seul** |
| afficher un texte | **un `string`** |
| juste réagir (quitter, recharger) | **rien** : `SendAsync("Mot")` |

> ⚠️ Ne jamais envoyer une entité sensible (`IdentityUser` → hash du mot de passe).
> Envoie seulement les champs utiles (DTO ou `new { ... }`).

---

## 4. RECETTE : écrire un `invoke` (React)

### Où regarder
| Je cherche | Je regarde |
|---|---|
| **Le nom** | la méthode dans le Hub : `public async Task NOM(...)` |
| **Quoi envoyer + l'ordre** | ses paramètres `(type1 a, type2 b, ...)` |
| **Où sont ces valeurs** | les `useState`, les paramètres de la fonction de clic, les propriétés d'un objet (`objet.id`) |
| **La valeur « rien »** | le `if` du serveur : `> 0` → envoyer `0` ; `!= null` → envoyer `null` |
| **S'il revient une valeur** | `Task` = rien ; `Task<X>` = un X |

### Étapes
```ts
function maFonction(/* ce qui vient du clic */) {
  if (!hubConnection) return;                                // 1. connexion prête ?
  // 2. préparer chaque valeur (objet.id, 0 si rien, .trim()...)
  hubConnection.invoke('NomExactMethode', val1, val2);       // 3. même nom, même ordre
  // 4. vider le champ / mettre à jour SEULEMENT si le serveur ne le fera pas
}
```

### Si la méthode retourne une valeur ou peut échouer
```ts
try {
  const resultat = await hubConnection.invoke('NomMethode', val);
} catch (e) {
  // HubException du serveur → message reçu ici
}
```

### Correspondance des types
| C# | TypeScript |
|---|---|
| `string` | `string` |
| `int` / `double` | `number` |
| `bool` | `boolean` |
| classe `X` | objet `{ champ: ... }` (champs en **minuscule**) |
| `int` alors que j'ai un objet | **`objet.id`** (pas l'objet entier !) |

---

## 5. RECETTE : écrire un `on` (React)

### Où regarder
| Je cherche | Je regarde |
|---|---|
| **Le nom** | le `SendAsync("NOM", ...)` du serveur (ou je le décide) |
| **Les paramètres** | ce qui est **après le nom** dans le `SendAsync` : un paramètre par donnée, même ordre |
| **Leur type** | le type C# → l'**interface** dans `models.ts` (champs en minuscule) |
| **Quoi mettre à jour** | les `useState` du composant : lequel correspond à cette donnée ? |
| **Comment le faire** | les fonctions déjà écrites (ex. `leaveChannel()` montre comment quitter) |

### Étapes
```ts
useEffect(() => {
  if (!hubConnection) return;                                   // 1. dans le useEffect

  hubConnection.on('NomEvenement', (data: MonType) => {         // 2. même nom ; 3. bon type
    setMaListe(data);                                           // 4. le bon set...
  });

  return () => { hubConnection.off('NomEvenement'); };          // 5. le off
}, [hubConnection]);
```

### Que mettre dans la fonction ?
| Le serveur envoie | Je fais |
|---|---|
| toute la **liste** | **remplacer** : `setListe(data)` |
| **un** élément | **ajouter** : `setListe(prev => [...prev, data])` |
| un **id à retirer** | **filtrer** : `setListe(prev => prev.filter(x => x.id !== id))` |
| **rien** (un signal) | **réinitialiser** : `setSelection(null)` |

> ⚠️ Toujours `prev =>` quand on part de l'ancienne valeur. Le `on` ne voit pas les nouvelles valeurs des variables.
> ⚠️ `[...prev, data]` avec `data` qui est **déjà une liste** donne un tableau dans un tableau ❌.

---

## 5 bis. Avec un DTO 📦

> Un DTO = **une petite classe qui contient seulement ce qu'on veut transporter**.
> Rien de magique : SignalR transforme **n'importe quel objet C#** en JSON, et inversement.
> Le DTO sert à **choisir les champs** (pas de données sensibles) et à avoir **des noms clairs**.

### La règle de base : qui fait le `new` ?
| Direction | Qui crée le DTO | Ce que tu écris |
|---|---|---|
| **Serveur → Client** (`SendAsync` → `on`) | **Toi**, dans le Hub (ou un service) | `new MonDTO { ... }`, puis `SendAsync("Mot", dto)` |
| **Client → Serveur** (`invoke` → méthode) | **SignalR**, automatiquement | un paramètre `MonDTO dto` dans la méthode, **pas de `new`** |
| **Client qui reçoit** (`on`) | **SignalR**, automatiquement | `(data: MonDTO) => ...` : `data` est déjà rempli |

### Le lien C# ↔ TypeScript (le plus important)
```csharp
public class MessageDTO                    // C#
{
    public int Id { get; set; }            // ⚠️ des PROPRIÉTÉS { get; set; }, pas des champs
    public string Auteur { get; set; } = "";
    public string Texte { get; set; } = "";
    public DateTime Date { get; set; }
}
```
```ts
export interface MessageDTO {              // TypeScript (models.ts)
  id: number;                              // Id     → id      (minuscule au début)
  auteur: string;                          // Auteur → auteur
  texte: string;
  date: string;                            // DateTime → string (format ISO)
}
```
| C# | JSON / TypeScript |
|---|---|
| `NomPropriete` | `nomPropriete` (1ʳᵉ lettre en minuscule) |
| `string` | `string` |
| `int` / `double` | `number` |
| `bool` | `boolean` |
| `DateTime` | `string` |
| `string?` / `int?` | `string \| null` / `number \| null` |
| `List<X>` | `X[]` |
| un autre DTO `X` | une autre interface `X` |

---

### A. Le serveur ENVOIE un DTO (le cas le plus fréquent)

**1. Créer la classe** (si elle n'existe pas). Ses champs = **ce dont le client a besoin**, rien de plus.
```csharp
public class UserDTO
{
    public string Email { get; set; } = "";
    public string UserId { get; set; } = "";
}
```

**2. Créer l'objet et l'envoyer**
```csharp
// UN seul objet
var dto = new UserDTO { Email = user.Email, UserId = user.Id };
await Clients.Caller.SendAsync("MonProfil", dto);

// UNE LISTE → .Select(...) : un DTO pour chaque élément
List<UserDTO> liste = UserHandler.UserConnections
    .Select(uc => new UserDTO { Email = uc.Key, UserId = uc.Value })
    .ToList();
await Clients.All.SendAsync("UsersList", liste);

// Depuis la BD
List<ChannelDTO> canaux = await _context.Channel
    .Select(c => new ChannelDTO { Id = c.Id, Titre = c.Title })
    .ToListAsync();
```

**3. Côté client : l'interface + le `on`**
```ts
export interface UserDTO { email: string; userId: string; }
```
```ts
hubConnection.on('UsersList', (data: UserDTO[]) => setUsersList(data));
```

**4. Adapter le code qui lit ces données** (les noms ont changé !)
```ts
user.key    → user.email
user.value  → user.userId
```
→ Fais **Cmd + Shift + F** sur l'ancien nom pour n'en oublier aucun.

---

### B. Le client ENVOIE un DTO (`invoke` avec un objet)

Utile quand il y a **beaucoup de paramètres**.

**Serveur** : la méthode reçoit **un seul paramètre**, le DTO. **Pas de `new`**, il est déjà rempli.
```csharp
public class EnvoyerMessageDTO
{
    public string Texte { get; set; } = "";
    public int CanalId { get; set; }
    public string? DestinataireId { get; set; }      // ? = peut être vide
}

public async Task EnvoyerMessage(EnvoyerMessageDTO dto)
{
    if (dto.Texte == "") throw new HubException("Message vide");
    if (dto.DestinataireId != null) { ... }
    else if (dto.CanalId != 0)      { ... }
}
```

**Client** : on envoie **un objet `{ }`** avec les mêmes champs **en minuscule**.
```ts
hubConnection.invoke('EnvoyerMessage', {
  texte: message,
  canalId: selectedChannel ? selectedChannel.id : 0,
  destinataireId: selectedUser?.userId ?? null
});
```
> ⚠️ L'**ordre** des champs dans `{ }` n'a **pas** d'importance. Ce sont les **noms** qui comptent.
> (À l'inverse, avec plusieurs paramètres séparés, c'est l'**ordre** qui compte.)

---

### C. Le DTO est RETOURNÉ par la méthode (`Task<MonDTO>`)

```csharp
public async Task<ResultatDTO> CreerPartie(int configId)
{
    ...
    return new ResultatDTO { PartieId = partie.Id, JoueurId = joueur.Id };
}
```
```ts
const resultat: ResultatDTO = await hubConnection.invoke('CreerPartie', 3);
console.log(resultat.partieId);
```

---

### D. Où le DTO peut être « caché »

Il n'y a pas toujours de `new` visible dans le Hub :
```csharp
await Clients.Group(g).SendAsync("PresenceList", _presenceService.GetPresences(id));
//                                               ^ retourne une List<PresenceDTO> créée DANS le service
```
→ Pour connaître la forme exacte : **Cmd + clic** sur la méthode, puis regarde son **type de retour** et la classe DTO.
→ Ou regarde le JSON réel : navigateur → **Network → WS → Messages**.

---

### E. Sans DTO, ça marche aussi

Si on ne demande pas de DTO, tu peux envoyer :
| Quoi | Exemple | Côté client |
|---|---|---|
| une valeur simple | `SendAsync("NewMessage", "texte")` | `(msg: string)` |
| plusieurs valeurs | `SendAsync("Info", email, 3)` | `(email: string, n: number)`, **même ordre** |
| un modèle existant non sensible | `SendAsync("ChannelsList", await _context.Channel.ToListAsync())` | `(data: Channel[])` |
| un objet anonyme | `SendAsync("Reaction", new { nom = email, emoji = "👍" })` | `(data: { nom: string; emoji: string })` |
| un dictionnaire | `SendAsync("UsersList", dico.ToList())` | `(data: { key: string; value: string }[])` |

---

### F. Les erreurs classiques avec un DTO

| Erreur | Ce qui se passe | Correction |
|---|---|---|
| Champ C# déclaré **sans** `{ get; set; }` | le champ **n'apparaît pas** dans le JSON | `public string X { get; set; }` |
| Interface TS avec une **majuscule** (`UserId`) | la valeur est `undefined` | `userId` (minuscule) |
| Nom différent entre C# et TS (`Titre` vs `title`) | `undefined` | mêmes noms (sauf la 1ʳᵉ lettre) |
| Oublier d'adapter le code après avoir changé les noms | affichage vide, `undefined` | chercher l'ancien nom partout |
| `invoke` avec un objet, mais la méthode attend **plusieurs** paramètres | erreur à l'appel | soit un DTO, soit des valeurs séparées : **pas les deux** |
| Faire `new MonDTO()` pour un paramètre reçu | on **écrase** ce que le client a envoyé | utiliser directement le paramètre |
| Envoyer l'entité de la BD au lieu d'un DTO | fuite de données (mot de passe...) | `.Select(x => new MonDTO { ... })` |
| La classe DTO n'est pas trouvée | « Cannot resolve symbol » | ajouter le bon `using` (le namespace du dossier DTO) |

---

## 6. BackgroundService + SignalR ⏱️

> Un service qui tourne **tout seul** (toutes les X secondes) et envoie des messages aux clients.
> **Personne ne l'appelle** : pas de `Context`, pas de `Caller`.

```csharp
public class MonBroadcaster : BackgroundService
{
    private readonly IHubContext<MonHub> _hubContext;      // ← pour envoyer depuis HORS du Hub
    private readonly IServiceScopeFactory _scopeFactory;   // ← pour utiliser les services Scoped (BD)

    public MonBroadcaster(IHubContext<MonHub> hubContext, IServiceScopeFactory scopeFactory)
    {
        _hubContext = hubContext;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))       // tant que le serveur tourne
        {
            using var scope = _scopeFactory.CreateScope();             // nouveau scope à chaque tour
            var service = scope.ServiceProvider.GetRequiredService<MonService>();

            var donnees = service.GetQuelqueChose();

            await _hubContext.Clients.All.SendAsync("MonEvenement", donnees, stoppingToken);
        }
    }
}
```
```csharp
// Program.cs
builder.Services.AddHostedService<MonBroadcaster>();
```

### Les 4 pièges
| Piège | Solution |
|---|---|
| Injecter `ApplicationDbContext` ou un service **Scoped** directement → plante au démarrage | `IServiceScopeFactory` + `CreateScope()` |
| Utiliser `MonHub` directement | `IHubContext<MonHub>` |
| `Clients.Caller` / `Context` | **n'existent pas** ici → `All`, `Group(...)`, `User(...)` |
| `while (true)` | `while (await timer.WaitForNextTickAsync(stoppingToken))` |

### Exemples de sujets possibles
- **Minuteur de tour** : toutes les secondes, si un joueur dépasse 60 s → finir son tour + `Group(partie)`.
- **Liste en direct** : toutes les 5 s → envoyer la liste des parties ou salles à `All`.
- **Nettoyage** : supprimer les éléments expirés + prévenir les clients concernés.
- **Compteur / horloge** : envoyer le nombre de connectés ou le temps restant.

---

## 7. Configuration (souvent déjà faite, mais à comprendre)

### Serveur : `Program.cs`
```csharp
builder.Services.AddSignalR();                         // activer SignalR
builder.Services.AddSingleton<MonServiceEnMemoire>();  // données partagées entre appels
builder.Services.AddScoped<MonServiceBD>();            // utilise le DbContext
builder.Services.AddHostedService<MonBroadcaster>();   // BackgroundService
...
app.UseCors(...);  app.UseAuthentication();  app.UseAuthorization();
app.MapHub<MonHub>("/monHub");                          // l'adresse du Hub
```

### Token JWT (le navigateur ne peut pas mettre d'en-tête sur un WebSocket → token dans l'URL)
```csharp
OnMessageReceived = context =>
{
    var accessToken = context.Request.Query["access_token"];                 // toujours ce nom
    if (!string.IsNullOrEmpty(accessToken) &&
        context.HttpContext.Request.Path.StartsWithSegments("/monHub"))      // ⚠️ = MapHub
        context.Token = accessToken;
    return Task.CompletedTask;
};
```
CORS : `WithOrigins("http://localhost:3000")` + `AllowCredentials()` (jamais `AllowAnyOrigin()` avec `AllowCredentials`).

### Client : la connexion
```ts
const conn = new HubConnectionBuilder()
  .withUrl('http://localhost:PORT/monHub', {                  // ⚠️ port + chemin = serveur
    accessTokenFactory: () => sessionStorage.getItem('token')! // ⚠️ même endroit que le login
  })
  .build();
// les .on AVANT start
await conn.start();
```

### Ce qui doit être IDENTIQUE à plusieurs endroits
| Valeur | Endroits |
|---|---|
| Chemin du Hub | `MapHub("/x")` = `StartsWithSegments("/x")` = `withUrl(".../x")` |
| Port | `launchSettings.json` = `withUrl("http://localhost:PORT")` |
| Stockage du token | `setItem('token')` au login = `getItem('token')` dans `accessTokenFactory` |

### Durées de vie
| Type | Combien | Pour quoi |
|---|---|---|
| **Hub** | recréé **à chaque appel** | ❌ ne jamais garder de données dans ses champs |
| **Singleton** | 1 pour toute l'app | données partagées en mémoire (qui attend, qui est connecté) → protéger avec `lock` |
| **Scoped** | 1 par appel | `DbContext` et services qui l'utilisent |

---

## 8. Méthode pour un TODO inconnu à l'examen 🔍

1. **Lis le TODO mot à mot**.
   - « joindre / quitter » = `Groups`.
   - « envoyer / mettre à jour / prévenir » = `SendAsync`.
   - « à cet utilisateur / ce canal / tous » = le destinataire.
2. **Lis les paramètres** de la méthode : c'est tout ce que le client envoie.
3. **Cherche l'`invoke`** qui appelle cette méthode dans le client : quelles valeurs ? (`0` = rien ?)
4. **Cherche tous les `.on(`** du client : quels mots sont déjà écoutés ? Quel type attendent-ils ?
5. **Regarde le code déjà écrit** par le prof :
   - un exemple de `SendAsync` à copier ;
   - une méthode privée jamais utilisée (`CreateGroupName`) = **indice** ;
   - une variable déclarée mais pas utilisée (`userTag`) = **indice**.
6. **Applique la recette** de la section 3 (serveur), 4 (`invoke`) ou 5 (`on`).

---

## 9. Débogage rapide 🐞

| Symptôme | Cause probable |
|---|---|
| Rien ne s'affiche, aucune erreur | nom `SendAsync` ≠ nom `on` · mauvais destinataire (`Caller` au lieu de `All`...) · pas dans le groupe |
| Console : `No client method with the name 'x' found` | le serveur envoie `x` mais aucun `on('x')` |
| Erreur `Method does not exist` | le nom de l'`invoke` ≠ le nom de la méthode du Hub |
| Erreur de conversion à l'`invoke` | mauvais type (objet au lieu de `objet.id`) · mauvais ordre · mauvais nombre de paramètres |
| 401 à la connexion | token absent · mauvais `getItem` · `StartsWithSegments` ≠ chemin du Hub |
| Tout s'affiche en double | `off` oublié dans le `return` du `useEffect` |
| La liste perd des éléments | `setX([...x, item])` au lieu de `setX(prev => [...prev, item])` |
| Erreur au démarrage du serveur avec BackgroundService | service Scoped injecté directement → `IServiceScopeFactory` |
| Message générique côté client au lieu du vrai message | `throw new Exception` → utiliser `throw new HubException` |

**Voir ce qui passe vraiment** : navigateur → **Cmd + Option + I** → **Network** → **WS** → la connexion → **Messages**.

---

## 10. Checklist finale ✅

**Serveur**
- [ ] Le Hub a `[Authorize]` si nécessaire, et il est mappé dans `Program.cs`.
- [ ] L'utilisateur vient de `Context.UserIdentifier`, pas d'un paramètre.
- [ ] Les vérifications font un `throw new HubException(...)`.
- [ ] `Groups` reçoit `Context.ConnectionId`.
- [ ] Chaque `SendAsync` a le **plus petit destinataire possible**.
- [ ] Chaque nom de `SendAsync` existe dans un `on` du client.
- [ ] Les données en mémoire sont dans un **Singleton** (pas dans le Hub).
- [ ] DTO envoyé : je fais le `new` (ou `.Select(x => new MonDTO {...})`) avec des propriétés `{ get; set; }`.
- [ ] DTO reçu en paramètre : **pas de `new`**, je l'utilise directement.
- [ ] BackgroundService : `IHubContext<>` + `IServiceScopeFactory` + `stoppingToken` + `AddHostedService`.

**Client**
- [ ] Chaque `invoke` a le **nom exact** d'une méthode du Hub, avec les valeurs **dans l'ordre** et du **bon type**.
- [ ] `if (!hubConnection) return;` avant chaque `invoke`.
- [ ] Chaque `on` est dans le `useEffect`, a le **même nom** que le `SendAsync`, et le bon `set...`.
- [ ] `prev =>` pour ajouter ou filtrer.
- [ ] Un `off` pour chaque `on`.
- [ ] Chaque DTO a son **interface** dans `models.ts`, avec les mêmes champs **en minuscule**.
- [ ] `invoke` avec un DTO : un objet `{ }` dont les noms = les propriétés C# en minuscule.
- [ ] Je ne mets pas à jour l'écran moi-même si un `on` va le faire (sinon doublons).
