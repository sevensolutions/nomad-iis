import BrowserOnly from '@docusaurus/BrowserOnly';
import JwtTokenGeneratorClient from './JwtTokenGeneratorClient';

export default function JwtTokenGenerator(): JSX.Element {
	return (
		<BrowserOnly fallback={<div>"Loading..."</div>}>
			{() => <JwtTokenGeneratorClient />}
		</BrowserOnly>
	);
}
