# 🧠 Feuille d’examen — SignalR et notifications

## 1. Le schéma principal à retenir

```text
CLIENT
  │
  │ invoke("Méthode", données)
  ↓
SERVEUR
  │
  │ effectue l’action / modifie les données
  │
  │ Clients.All / Clients.Group
  │ .SendAsync("Notification", données)
  ↓
CLIENT
  │
  │ .on("Notification", data => ...)
  ↓
setState(...)
```

| Code | Signification |
|---|---|
| `invoke()` | Client → serveur |
| `SendAsync()` | Serveur → client(s) |
| `.on()` | Le client écoute une notification |
| `Clients.All` | Tous les clients connectés |
| `Clients.Group(...)` | Les clients membres du groupe |
| `Groups.AddToGroupAsync()` | Ajouter un client à un groupe |
| `Groups.RemoveFromGroupAsync()` | Retirer un client d’un groupe |

## 2. Côté client : `newHubConnection`

Le code client est écrit une fois. Chaque personne qui ouvre l’application exécute ce code et crée sa propre connexion.

```tsx
function connectToHub() {
    const newHubConnection = new HubConnectionBuilder()
        .withUrl("...")
        .build();

    // Déclarer les notifications ici, avant de démarrer la connexion.

    newHubConnection.start();
    setHubConnection(newHubConnection);
}
```

## 3. Notifications côté client

Le nom passé à `SendAsync` doit correspondre exactement au nom passé à `.on()`.

```csharp
await Clients.All.SendAsync("Notification", notification);
```

```tsx
newHubConnection.on("Notification", (data) => {
    // Utiliser les données reçues.
});
```

```text
Serveur : SendAsync("Notification", notification)
                         ↓
Client :  on("Notification", data => ...)
                         ↓
                   utiliser data
```

## 4. Exemples d’événements

### Compteur d’utilisateurs : `UserCount`

Serveur :

```csharp
await Clients.All.SendAsync(
    "UserCount",
    UserHandler.UserConnections.Count
);
```

Client :

```tsx
newHubConnection.on("UserCount", (data) => {
    setUserCount(data);
});
```

`"UserCount"` est le nom de la notification; `data` contient le nombre envoyé par le serveur.

### Liste de channels : `ChannelList`

Serveur :

```csharp
await Clients.All.SendAsync(
    "ChannelList",
    _context.Channel.ToList()
);
```

Client :

```tsx
newHubConnection.on("ChannelList", (data) => {
    setChannelsList(data);
});
```

Le serveur envoie la liste et le client met à jour son état.

### Nouveau message : `NewMessage`

Serveur :

```csharp
await Clients.All.SendAsync("NewMessage", message);
```

Client :

```tsx
newHubConnection.on("NewMessage", (data) => {
    setMessage(data);
});
```

### Notification sans données : `LeaveChannel`

Serveur :

```csharp
await Clients.Group(groupName).SendAsync("LeaveChannel");
```

Client :

```tsx
newHubConnection.on("LeaveChannel", () => {
    setSelectedChannel(null);
});
```

Pas de paramètre `data` : le serveur n’envoie aucune donnée après le nom de l’événement.

## 5. `invoke()` : appeler le serveur

Un bouton client peut appeler une méthode du hub :

```tsx
function onButtonClick() {
    hubConnection!.invoke("IncrementCounter");
}
```

Le sens est **client → serveur**. Le nom correspond à la méthode côté serveur :

```csharp
public async Task IncrementCounter()
{
    counter++;

    await Clients.All.SendAsync("CounterUpdated", counter);
}
```

## 6. Compteurs synchronisés

Quand une personne clique, le serveur modifie la valeur puis envoie la nouvelle valeur à tout le monde.

Client :

```tsx
newHubConnection.on("CounterUpdated", (data) => {
    setCounter(data);
});

function onButtonClick() {
    hubConnection!.invoke("IncrementCounter");
}
```

Serveur :

```csharp
public async Task IncrementCounter()
{
    counter++;

    await Clients.All.SendAsync("CounterUpdated", counter);
}
```

```text
Un client clique
      ↓
invoke("IncrementCounter")
      ↓
Le serveur fait counter++
      ↓
Clients.All.SendAsync("CounterUpdated", counter)
      ↓
Tous les clients reçoivent la même valeur avec .on(...)
```

Le serveur conserve la valeur de référence. Un simple `setCounter(counter + 1)` ne mettrait à jour que l’interface du client qui a cliqué.

## 7. `Clients.All` ou `Clients.Group`

Envoyer à tous les clients connectés :

```csharp
await Clients.All.SendAsync("Notification", notification);
```

Envoyer seulement aux membres d’un groupe :

```csharp
await Clients.Group(groupName)
    .SendAsync("Notification", notification);
```

## 8. Gérer les groupes

Ajouter la connexion actuelle à un groupe :

```csharp
await Groups.AddToGroupAsync(
    Context.ConnectionId,
    groupName
);
```

Retirer la connexion actuelle d’un groupe :

```csharp
await Groups.RemoveFromGroupAsync(
    Context.ConnectionId,
    groupName
);
```

Envoyer une notification aux membres du groupe :

```csharp
await Clients.Group(groupName)
    .SendAsync("Notification", data);
```

## 9. Condition `if (bouton)`

Si l’énoncé dit d’envoyer la notification lorsque le bouton vaut `true` :

```csharp
if (bouton)
{
    await Clients.Group(groupName)
        .SendAsync("Notification", notification);
}
```

```text
bouton vaut true
      ↓
le code entre dans le if
      ↓
Clients.Group(groupName).SendAsync(...)
      ↓
les membres du groupe reçoivent la notification
```

Si `bouton` vaut `false`, le code dans le `if` n’est pas exécuté. N’invente pas un autre comportement pour `false` si l’énoncé ne le demande pas.

## 10. Exemple : channel supprimé

Le serveur peut envoyer plusieurs événements distincts :

```csharp
await Clients.Group(groupName)
    .SendAsync("NewMessage", "Le channel a été détruit");

await Clients.Group(groupName)
    .SendAsync("LeaveChannel");

await Clients.All
    .SendAsync("ChannelsList", channels);
```

Le client écoute chaque nom d’événement :

```tsx
newHubConnection.on("NewMessage", (data) => {
    setMessage(data);
});

newHubConnection.on("LeaveChannel", () => {
    setSelectedChannel(null);
});

newHubConnection.on("ChannelsList", (data) => {
    setChannelsList(data);
});
```

## 11. Like et notification au groupe

Si l’exercice dit que le clic sur « J’aime » déclenche une notification au groupe lorsque le booléen est vrai :

```csharp
if (isLiked)
{
    await Clients.Group(groupName)
        .SendAsync("Notification", notification);
}
```

Côté client :

```tsx
newHubConnection.on("Notification", (data) => {
    // Mettre à jour l’interface avec la notification reçue.
});
```

```text
isLiked == true
      ↓
if (isLiked)
      ↓
Clients.Group(...).SendAsync("Notification", ...)
      ↓
.on("Notification", data => ...)
```

## 12. Résoudre les TODO

### TODO côté client

1. Chercher dans le serveur le `SendAsync("Nom", ...)` correspondant.
2. Reprendre exactement ce nom dans `.on("Nom", ...)`.
3. Regarder les données envoyées après le nom : elles deviennent le paramètre reçu, souvent `data`.
4. Mettre à jour l’état ou l’interface selon l’exercice.

Exemple serveur :

```csharp
await Clients.All.SendAsync("Notification", notification);
```

TODO côté client :

```tsx
newHubConnection.on("Notification", (data) => {
    setNotification(data);
});
```

### TODO côté serveur

Commencer par déterminer qui doit recevoir :

```csharp
Clients.All                 // tous les clients
Clients.Group(groupName)    // les membres d’un groupe
Clients.User(userId)        // un utilisateur
```

Puis envoyer l’événement et les données demandées :

```csharp
await Clients.Group(groupName)
    .SendAsync("NomDeLEvenement", donnees);
```

## 13. Réflexes à retenir

### `invoke` : client vers serveur

```tsx
hubConnection.invoke("CreateChannel", title);
```

### `SendAsync` : serveur vers client(s)

```csharp
await Clients.All.SendAsync("ChannelList", channels);
```

### `.on` : le client écoute le serveur

```tsx
newHubConnection.on("ChannelList", (data) => {
    setChannelsList(data);
});
```

### Scénario complet

```text
CLIENT : clic sur un bouton
      ↓
invoke("ActionServeur", données)
      ↓
SERVEUR : exécute l’action
      ↓
if (condition)
      ↓
Clients.Group(...) ou Clients.All
      ↓
SendAsync("Notification", données)
      ↓
CLIENT : .on("Notification", data => ...)
      ↓
setState(data)
```

## Les 10 choses à savoir pour l’examen

1. `newHubConnection` crée la connexion SignalR côté client.
2. `invoke()` appelle une méthode du serveur.
3. `SendAsync()` envoie un événement du serveur vers des clients.
4. `.on()` écoute un événement côté client.
5. `Clients.All` vise tous les clients connectés.
6. `Clients.Group(groupName)` vise les membres du groupe.
7. `AddToGroupAsync()` fait rejoindre un groupe à une connexion.
8. `RemoveFromGroupAsync()` retire une connexion d’un groupe.
9. `if (bouton)` exécute son bloc seulement si `bouton` vaut `true`.
10. `SendAsync("X", data)` correspond à `.on("X", data => ...)`.

## Le réflexe ultime

Avant d’écrire le code, demande-toi : **qui parle à qui ?**

- Client → serveur : `invoke()`
- Serveur → tous les clients : `Clients.All.SendAsync(...)`
- Serveur → groupe : `Clients.Group(...).SendAsync(...)`
- Client reçoit l’événement : `.on(...)`
- Action soumise à une condition : `if (...)`

Avec ce raisonnement, tu peux reconstruire le code même si les noms et le contexte changent.
