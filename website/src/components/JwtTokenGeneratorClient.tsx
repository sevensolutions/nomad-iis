import { useState } from "react";
import MDXContent from '@theme/MDXContent';
import CodeBlock from '@theme/CodeBlock';

export default function JwtTokenGeneratorClient() {
	const [secret, setSecret] = useState<string>("VETkEPWkaVTxWf7J4Mm20KJWOx2cK4S7VvoP3ybjh6fr9P9PXvyhlY8HV2Jgxm2O");
	const [token, setToken] = useState<string>();

	return (
		<div style={{ display: "flex", flexDirection: "column", gap: "0.5em" }}>

			<MDXContent>
				JWT Tokens provide a more flexible way of securing the Management API.<br />

				Generate a random secret and fill it in here:
			</MDXContent>

			<div style={{ display: "flex", gap: "0.5em" }}>
				<label>JWT Secret:</label>
				<input type="text" value={secret} onChange={ev => setSecret(ev.target.value)} style={{ flex: 1 }}></input>
			</div>

			<MDXContent>
				Specify the secret using the `--management-api-jwt-secret`-argument as shown:
			</MDXContent>

			<CodeBlock language="hcl">
				{
					`plugin "nomad_iis" {
  args = [
    "--management-api-port=5004",
		# highlight-next-line
    "--management-api-jwt-secret=${secret}"
  ]
  config {
    enabled = true
  }
}`
				}
			</CodeBlock>

			<button onClick={generateToken}>Generate</button>

			<span>{token}</span>
		</div>
	);

	async function generateToken() {
		try {
			var claims = { a: 1 };
			const t = await createToken(claims, secret);

			setToken(t);
		}
		catch (ex: any) {
			setToken(`Error: ${ex}`);
		}
	}

	async function createToken(payload, key) {
		var header = { typ: 'JWT', alg: 'HS256' };

		var segments = [];
		segments.push(encodeBase64Url(JSON.stringify(header)));
		segments.push(encodeBase64Url(JSON.stringify(payload)));

		var footer = await sign(segments.join('.'), key);

		segments.push(footer);

		return segments.join('.');
	}

	async function sign(data: any, secret: string) {
		var encoder = new TextEncoder();
		const encodedSecret = encoder.encode(secret);

		const cryptoKey = await window.crypto.subtle.importKey(
			"raw", //can be "jwk" or "raw"
			encodedSecret,
			{   //this is the algorithm options
				name: "HMAC",
				hash: { name: "SHA-256" }, //can be "SHA-1", "SHA-256", "SHA-384", or "SHA-512"
				//length: 256, //optional, if you want your key length to differ from the hash function's block length
			},
			true, //whether the key is extractable (i.e. can be used in exportKey)
			["sign", "verify"] //can be any combination of "sign" and "verify"
		);

		var jsonString = JSON.stringify(data);
		var encodedData = encoder.encode(jsonString);

		const token = await window.crypto.subtle.sign(
			{
				name: "HMAC",
			},
			cryptoKey,
			encodedData
		);

		var u8 = new Uint8Array(token);
		var b64encoded = encodeBase64Url(String.fromCharCode.apply(null, u8));

		return b64encoded;
	}

	function encodeBase64Url(data) {
		return btoa(data).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
	}
}
