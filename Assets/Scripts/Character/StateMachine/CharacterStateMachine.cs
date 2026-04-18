using Character.Intent;

namespace Character.StateMachine
{
public sealed class CharacterStateMachine
{
  public ICharacterState CurrentState { get; private set; }

  public void Initialize(ICharacterState initialState)
  {
    CurrentState = initialState;
    CurrentState.Enter();
  }
  
  public void Tick(CharacterIntent intent, float deltaTime)
  {
    CurrentState.Tick(intent, deltaTime);
  }

  public void ChangeState(ICharacterState newState)
  {
    if(newState == null || newState.Id == CurrentState.Id) return;
    CurrentState?.Exit();
    CurrentState = newState;
    CurrentState?.Enter();
  }
}
}
