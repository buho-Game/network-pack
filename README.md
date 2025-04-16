# buho Network Pack

A Unity package providing network utilities for scene transitions in multiplayer games.

## Features

- Networked scene transitions with fade effects
- Local scene transitions
- Synchronized loading across all clients
- Customizable transition parameters
- Support for both single and additive scene loading

## Installation

### Using Git URL

You can add this package to your project by adding it through the Package Manager using Git URL:

1. Open Unity and go to Window > Package Manager
2. Click the "+" button in the top-left corner
3. Select "Add package from git URL..."
4. Enter: `https://github.com/buho-Game/network-pack.git`
5. Click Add

### Manual Installation

You can also download the repository and place it in your project's `Packages` folder.

## Dependencies

This package requires:

- Unity Netcode for GameObjects

## Usage

### Basic Networked Scene Transition

```csharp
using com.buho.NetworkPack.Scene;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    void Start()
    {
        // Register for events
        SceneTransitionManager.Instance.OnAllClientsReady += HandleAllClientsReady;
    }

    public void LoadGameScene()
    {
        // Load a networked scene with fade transition
        SceneTransitionManager.Instance.LoadNetworkedScene("GameScene");
    }

    private void HandleAllClientsReady()
    {
        // All clients have loaded, fade out transition
        SceneTransitionManager.Instance.FadeOut();
    }
}
```

### Local Scene Transition

```csharp
using com.buho.NetworkPack.Scene;
using UnityEngine;

public class MenuManager : MonoBehaviour
{
    public void LoadOptionsMenu()
    {
        // Load a local scene with custom hold duration
        SceneTransitionManager.Instance.LocalScreenTransition(
            "OptionsMenu",
            null,
            0.2f
        );
    }

    public void SimpleTransition()
    {
        // Simple fade transition with callback
        SceneTransitionManager.Instance.FadeTransition(
            () => Debug.Log("Mid-transition action"),
            0.5f
        );
    }
}
```

## API Reference

### SceneTransitionManager

#### Properties

- `FadeDuration` - Gets the duration of fade transitions in seconds
- `IsTransitioning` - Gets whether a transition is currently in progress
- `NetworkObj` - Gets the NetworkObject associated with this manager

#### Events

- `OnLoadingStarted` - Triggered when a scene loading operation starts
- `OnLoadingCompleted` - Triggered when scene loading is completed
- `OnAllClientsReady` - Triggered when all network clients have completed loading

#### Methods

- `LoadNetworkedScene(string sceneName)` - Load a networked scene with fade transition
- `LoadNetworkedScene(string sceneName, LoadSceneMode loadSceneMode)` - Load a networked scene with specific load mode
- `FadeOut()` - Fades out the transition overlay
- `LocalScreenTransition(string targetSceneName, Action onMidTransition, float holdDuration)` - Performs a local screen transition
- `FadeTransition(Action onMidTransition, float holdDuration)` - Performs a simple fade transition
- `FadeToBlack()` - Immediately fades to black without fading back out
- `SetFadeColor(Color color)` - Sets or changes the fade color for transitions

## License

[MIT License](LICENSE)
