using System.Collections.Generic;

namespace VideoBatchMerger;

internal sealed class TransitionPlan
{
	public List<TransitionSpec> Effects = new List<TransitionSpec>();

	public bool RandomOrder;

	public bool LockSingleEffectPerOutput;

	public double DurationSeconds;

	public bool Enabled => Effects.Count > 0;

	public static TransitionPlan FromSingle(TransitionSpec effect)
	{
		TransitionPlan transitionPlan = new TransitionPlan();
		transitionPlan.DurationSeconds = effect?.DurationSeconds ?? 1.0;
		if (effect != null && effect.Enabled)
		{
			transitionPlan.Effects.Add(effect);
		}
		return transitionPlan;
	}
}
