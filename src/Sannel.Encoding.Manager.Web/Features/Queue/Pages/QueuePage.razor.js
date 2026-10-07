export function initScrollSentinel(element, dotNetRef) {
	const observer = new IntersectionObserver(entries => {
		if (entries[0].isIntersecting) {
			dotNetRef.invokeMethodAsync('OnScrolledToBottom');
		}
	}, { threshold: 0.1 });
	observer.observe(element);
	return observer;
}

export function disposeObserver(observer) {
	observer.disconnect();
}
