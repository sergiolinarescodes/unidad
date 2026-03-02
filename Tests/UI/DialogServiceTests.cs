using NUnit.Framework;
using Unidad.Core.Tests.Tests.TestUtilities;
using Unidad.Core.UI.Dialog;
using Unidad.Core.UI.Events;
using Unidad.Core.UI.TextAnimation;
using Unidad.Core.UI.TextAnimation.ElementAnimation;

namespace Unidad.Core.Tests.Tests.UI
{
    [TestFixture]
    public class DialogServiceTests
    {
        private MockEventBus _eventBus;
        private DialogService _service;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new MockEventBus();
            var textAnimation = new TextAnimationService();
            var elementAnimator = new ElementAnimator();
            _service = new DialogService(_eventBus, textAnimation, elementAnimator, null);
        }

        [Test]
        public void Show_PublishesDialogShownEvent()
        {
            var def = new DialogDefinition("Title", "Body",
                new[] { new DialogButton("OK") }, id: "test");

            _service.Show(def);

            Assert.That(_eventBus.HasEventOfType<DialogShownEvent>(), Is.True);
            Assert.That(_eventBus.GetPublishedEvent<DialogShownEvent>().DialogId, Is.EqualTo("test"));
        }

        [Test]
        public void Show_SetsHasActiveDialog()
        {
            Assert.That(_service.HasActiveDialog, Is.False);

            var def = new DialogDefinition("Title", "Body",
                new[] { new DialogButton("OK") });
            _service.Show(def);

            Assert.That(_service.HasActiveDialog, Is.True);
        }

        [Test]
        public void DismissCurrent_ClearsActiveDialog()
        {
            var def = new DialogDefinition("Title", "Body",
                new[] { new DialogButton("OK") });
            _service.Show(def);
            _service.DismissCurrent();

            Assert.That(_service.HasActiveDialog, Is.False);
        }

        [Test]
        public void DismissCurrent_InvokesResultCallback()
        {
            DialogResult receivedResult = null;
            var def = new DialogDefinition("Title", "Body",
                new[] { new DialogButton("OK") });

            _service.Show(def, result => receivedResult = result);
            _service.DismissCurrent();

            Assert.That(receivedResult, Is.Not.Null);
            Assert.That(receivedResult.ButtonId, Is.EqualTo("dismissed"));
        }

        [Test]
        public void DismissCurrent_PublishesDismissedEvent()
        {
            var def = new DialogDefinition("Title", "Body",
                new[] { new DialogButton("OK") }, id: "test");

            _service.Show(def);
            _service.DismissCurrent();

            Assert.That(_eventBus.HasEventOfType<DialogDismissedEvent>(), Is.True);
        }

        [Test]
        public void Show_QueuesSecondDialog()
        {
            var def1 = new DialogDefinition("Title 1", "Body 1",
                new[] { new DialogButton("OK") }, id: "d1");
            var def2 = new DialogDefinition("Title 2", "Body 2",
                new[] { new DialogButton("OK") }, id: "d2");

            _service.Show(def1);
            _service.Show(def2);

            // Only first dialog event should fire
            Assert.That(_eventBus.CountEventsOfType<DialogShownEvent>(), Is.EqualTo(1));
            Assert.That(_eventBus.GetPublishedEvent<DialogShownEvent>().DialogId, Is.EqualTo("d1"));
        }

        [Test]
        public void DismissCurrent_ShowsNextQueued()
        {
            var def1 = new DialogDefinition("Title 1", "Body 1",
                new[] { new DialogButton("OK") }, id: "d1");
            var def2 = new DialogDefinition("Title 2", "Body 2",
                new[] { new DialogButton("OK") }, id: "d2");

            _service.Show(def1);
            _service.Show(def2);
            _service.DismissCurrent();

            // Second dialog should now be shown
            Assert.That(_service.HasActiveDialog, Is.True);
            Assert.That(_eventBus.CountEventsOfType<DialogShownEvent>(), Is.EqualTo(2));
        }

        [Test]
        public void DismissAll_ClearsQueueAndActive()
        {
            var def1 = new DialogDefinition("Title 1", "Body 1",
                new[] { new DialogButton("OK") });
            var def2 = new DialogDefinition("Title 2", "Body 2",
                new[] { new DialogButton("OK") });

            _service.Show(def1);
            _service.Show(def2);
            _service.DismissAll();

            Assert.That(_service.HasActiveDialog, Is.False);
        }
    }
}
