import { useCallback, useState } from "react";
import MDXContent from '@theme/MDXContent';
import CodeBlock from '@theme/CodeBlock';

export default function JwtTokenGeneratorClient() {
	const [secret, setSecret] = useState<string>("VETkEPWkaVTxWf7J4Mm20KJWOx2cK4S7VvoP3ybjh6fr9P9PXvyhlY8HV2Jgxm2O");
	const [token, setToken] = useState<string>("");
	const [namespace, setNamespace] = useState<string>("");
	const [jobId, setJobId] = useState<string>();
	const [allocId, setAllocId] = useState<string>();
	const [filesystemAccess, setFilesystemAccess] = useState<boolean>(true);
	const [appPoolLifecycle, setAppPoolLifecycle] = useState<boolean>(true);
	const [screenshots, setScreenshots] = useState<boolean>(true);
	const [processDumps, setProcessDumps] = useState<boolean>(true);


	return (
		<div style={{ display: "flex", flexDirection: "column", gap: "0.5em" }}>
			<h3>Configure the Plugin</h3>

			<MDXContent>
				JWT Tokens provide a more flexible way of securing the Management API.<br />

				Generate a random secret and fill it in here:
			</MDXContent>

			<div style={{ display: "flex", gap: "0.5em" }}>
				<label>JWT Secret:</label>
				<input type="text" value={secret} onChange={ev => { setSecret(ev.target.value); generateToken(); }} style={{ flex: 1 }}></input>
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

			<h3>Generate a JWT Token</h3>

			<div style={{ display: "flex", gap: "1em" }}>
				<div style={{ flex: 1 }}>
					<h4>Limit to Job</h4>

					<div style={{ display: "flex", gap: "0.5em" }}>
						<label>Namespace:</label>
						<input type="text" value={namespace} onChange={ev => { setNamespace(ev.target.value); generateToken(); }} style={{ flex: 1 }}></input>
					</div>
					<div style={{ display: "flex", gap: "0.5em" }}>
						<label>Job Id:</label>
						<input type="text" value={jobId} onChange={ev => { setJobId(ev.target.value); generateToken(); }} style={{ flex: 1 }}></input>
					</div>
					<div style={{ display: "flex", gap: "0.5em" }}>
						<label>Alloc Id:</label>
						<input type="text" value={allocId} onChange={ev => { setAllocId(ev.target.value); generateToken(); }} style={{ flex: 1 }}></input>
					</div>
				</div>
				<div style={{ flex: 1 }}>
					<h4>Limit Capabilities</h4>
					
					<div style={{ display: "flex", gap: "0.5em" }}>
						<input id="cbCapability1" type="checkbox" checked={filesystemAccess} onChange={ev => { setFilesystemAccess(ev.target.checked); generateToken(); }}></input>
						<label htmlFor="cbCapability1">Filesystem Access</label>
					</div>
					<div style={{ display: "flex", gap: "0.5em" }}>
						<input id="cbCapability2" type="checkbox" checked={appPoolLifecycle} onChange={ev => { setAppPoolLifecycle(ev.target.checked); generateToken(); }}></input>
						<label htmlFor="cbCapability2">Application Pool Lifecycle Management</label>
					</div>
					<div style={{ display: "flex", gap: "0.5em" }}>
						<input id="cbCapability3" type="checkbox" checked={screenshots} onChange={ev => { setScreenshots(ev.target.checked); generateToken(); }}></input>
						<label htmlFor="cbCapability3">Screenshots</label>
					</div>
					<div style={{ display: "flex", gap: "0.5em" }}>
						<input id="cbCapability4" type="checkbox" checked={processDumps} onChange={ev => { setProcessDumps(ev.target.checked); generateToken(); }}></input>
						<label htmlFor="cbCapability4">Process Dumps</label>
					</div>
				</div>
			</div>

			<span>Your Token:</span>
			<span>{token}</span>
		</div>
	);

	async function generateToken() {
		try {
			var claims: any = {
				capabilities: []
			};

			if (namespace)
				claims.namespace = namespace;
			if (jobId)
				claims.jobId = jobId;
			if (allocId)
				claims.allocId = allocId;

			if (filesystemAccess)
				claims.capabilities.push("filesystemAccess");
			if (appPoolLifecycle)
				claims.capabilities.push("appPoolLifecycle");
			if (screenshots)
				claims.capabilities.push("screenshots");
			if (processDumps)
				claims.capabilities.push("procDump");

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
